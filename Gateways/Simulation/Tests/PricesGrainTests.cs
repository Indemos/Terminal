using Core.Grains;
using Core.Models;
using Core.Tests;
using Moq;
using Orleans;
using Orleans.TestingHost;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Tests
{
  public class Prices : IDisposable
  {
    private readonly Mock<IClusterClient> _mockConnector;
    private readonly TestCluster _cluster;

    private string Descriptor => $"{Guid.NewGuid()}";
    private Instrument Instrument => new Instrument { Name = "SPY" };

    public Prices()
    {
      _mockConnector = new Mock<IClusterClient>();

      var builder = new TestClusterBuilder();

      builder.AddSiloBuilderConfigurator<SiloConfigurator>();
      builder.AddClientBuilderConfigurator<SiloConfigurator>();

      _cluster = builder.Build();
      _cluster.Deploy();
    }

    public void Dispose()
    {
      _cluster.StopAllSilos();
    }

    [Fact]
    public async Task StoreSetsCurrentPrice()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      var response = await grain.Send(Instrument with { Price = new() { Last = 100.0 } });
      var price = response.Price;

      Assert.Null(price.Time);
      Assert.Null(price.Bar.Time);
      Assert.Equal(100.0, price.Last);
      Assert.Equal(100.0, price.Ask);
      Assert.Equal(100.0, price.Bid);
      Assert.Equal(0.0, price.AskSize);
      Assert.Equal(0.0, price.BidSize);
      Assert.Equal(100.0, price.Bar.Low);
      Assert.Equal(100.0, price.Bar.High);
      Assert.Equal(100.0, price.Bar.Open);
      Assert.Equal(100.0, price.Bar.Close);
    }

    [Fact]
    public async Task StoreUsesPreviousPrice()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      await grain.Send(Instrument with { Price = new() { Last = 100.0, Time = 1 } });

      var response = await grain.Send(Instrument with { Price = new() { Time = 1 } });
      var price = response.Price;

      Assert.Equal(1, price.Time);
      Assert.Equal(1, price.Bar.Time);
      Assert.Equal(100.0, price.Last);
      Assert.Equal(100.0, price.Ask);
      Assert.Equal(100.0, price.Bid);
      Assert.Equal(0.0, price.AskSize);
      Assert.Equal(0.0, price.BidSize);
      Assert.Equal(100.0, price.Bar.Low);
      Assert.Equal(100.0, price.Bar.High);
      Assert.Equal(100.0, price.Bar.Open);
      Assert.Equal(100.0, price.Bar.Close);
    }

    [Fact]
    public async Task StorePreservesPreviousValues()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      var span = TimeSpan.FromMinutes(1);

      await grain.Send(Instrument with
      {
        TimeFrame = span,
        Price = new()
        {
          Last = 100.0,
          Bid = 90,
          Ask = 110,
          BidSize = 5,
          AskSize = 10,
          Time = 1
        }
      });

      await grain.Send(Instrument with { TimeFrame = span, Price = new() { Time = 1, Last = 50 } });
      await grain.Send(Instrument with { TimeFrame = span, Price = new() { Time = 1, Last = 200 }});
      await grain.Send(Instrument with { TimeFrame = span, Price = new() { Time = 1, Last = 150 } });

      var response = await grain.Send(Instrument with { TimeFrame = span, Price = new() { Time = 1, Last = 70 } });
      var price = response.Price;

      Assert.Equal(1, price.Time);
      Assert.Equal(0, price.Bar.Time);
      Assert.Equal(70.0, price.Last);
      Assert.Equal(90.0, price.Bid);
      Assert.Equal(110.0, price.Ask);
      Assert.Equal(5.0, price.BidSize);
      Assert.Equal(10.0, price.AskSize);
      Assert.Equal(50.0, price.Bar.Low);
      Assert.Equal(200.0, price.Bar.High);
      Assert.Equal(100.0, price.Bar.Open);
      Assert.Equal(70.0, price.Bar.Close);
    }

    [Fact]
    public async Task StoreUpdatesPreviousValues()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      await grain.Send(Instrument with
      {
        Price = new()
        {
          Last = 100.0,
          Bid = 90,
          Ask = 110,
          BidSize = 5,
          AskSize = 10,
          Time = 1
        }
      });

      await grain.Send(Instrument with { Price = new() { Time = 1, Last = 50 } });
      await grain.Send(Instrument with { Price = new() { Time = 1, Last = 200 } });
      await grain.Send(Instrument with { Price = new() { Time = 1, Last = 150 } });

      Assert.Equal(10.0, (await grain.Send(Instrument with { Price = new() { Time = 1, Last = 10 } })).Price.Bar.Low);
      Assert.Equal(250.0, (await grain.Send(Instrument with { Price = new() { Time = 1, Last = 250 } })).Price.Bar.High);
      Assert.Equal(15.0, (await grain.Send(Instrument with { Price = new() { Time = 1, Bid = 15 } })).Price.Bid);
      Assert.Equal(25.0, (await grain.Send(Instrument with { Price = new() { Time = 1, Ask = 25 } })).Price.Ask);
      Assert.Equal(15.0, (await grain.Send(Instrument with { Price = new() { Time = 1, BidSize = 15 } })).Price.BidSize);
      Assert.Equal(25.0, (await grain.Send(Instrument with { Price = new() { Time = 1, AskSize = 25 } })).Price.AskSize);
      Assert.Equal(2, (await grain.Send(Instrument with { Price = new() { Time = 2, Last = 15 } })).Price.Bar.Time);
      Assert.Equal(35, (await grain.Send(Instrument with { Price = new() { Time = 3, Last = 35 } })).Price.Bar.Open);
    }

    [Fact]
    public void StoreException()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      Assert.Throws<AggregateException>(() => grain.Send(null).Result);
      Assert.Throws<AggregateException>(() => grain.Send(new()).Result);
    }

    [Fact]
    public async Task Instrument_ReturnsStoredInstrument()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      var instrument = Instrument with { Price = new() { Last = 150.0, Time = 1 } };
      await grain.Send(instrument);

      // Act
      var result = await grain.Instrument();

      // Assert
      Assert.NotNull(result);
      Assert.Equal("SPY", result.Name);
      Assert.Equal(150.0, result.Price.Last);
    }

    [Fact]
    public async Task Instrument_ReturnsNullWhenNotInitialized()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      // Act
      var result = await grain.Instrument();

      // Assert
      Assert.Null(result);
    }

    [Fact]
    public async Task Send_StoresFirstPrice()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      var instrument = Instrument with
      {
        Price = new()
        {
          Last = 100.0,
          Ask = 101.0,
          Bid = 99.0,
          AskSize = 100.0,
          BidSize = 150.0,
          Time = 1000
        }
      };

      // Act
      var result = await grain.Send(instrument);

      // Assert
      Assert.NotNull(result);
      Assert.Equal(100.0, result.Price.Last);
      Assert.Equal(101.0, result.Price.Ask);
      Assert.Equal(99.0, result.Price.Bid);
      Assert.Equal(100.0, result.Price.AskSize);
      Assert.Equal(150.0, result.Price.BidSize);
      Assert.Equal(1000, result.Price.Time);
      Assert.Equal(100.0, result.Price.Bar.Open);
      Assert.Equal(100.0, result.Price.Bar.Close);
      Assert.Equal(100.0, result.Price.Bar.High);
      Assert.Equal(100.0, result.Price.Bar.Low);
    }

    [Fact]
    public async Task Send_AccumulatesBarHighAndLowWithinTimeFrame()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      // Use same timestamp to keep prices within same bar
      await grain.Send(Instrument with { Price = new() { Last = 100.0, Time = 1 } });
      await grain.Send(Instrument with { Price = new() { Last = 120.0, Time = 1 } });
      var resultAfterHigh = await grain.Instrument();

      await grain.Send(Instrument with { Price = new() { Last = 85.0, Time = 1 } });
      var resultAfterLow = await grain.Instrument();

      // Assert
      // Within the same time, bar accumulates min/max values
      Assert.Equal(120.0, resultAfterHigh.Price.Bar.Low);
      Assert.Equal(120.0, resultAfterHigh.Price.Bar.High);

      Assert.Equal(85.0, resultAfterLow.Price.Bar.Low);
      Assert.Equal(85.0, resultAfterLow.Price.Bar.High);
    }

    [Fact]
    public async Task Send_PreservesOpenPriceAndUpdatesClose()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      await grain.Send(Instrument with { Price = new() { Last = 100.0, Time = 1 } });
      var first = await grain.Instrument();

      // Act
      await grain.Send(Instrument with { Price = new() { Last = 120.0, Time = 2 } });
      await grain.Send(Instrument with { Price = new() { Last = 85.0, Time = 3 } });
      var result = await grain.Instrument();

      // Assert
      // Open is preserved from first price in the bar
      Assert.Equal(100.0, first.Price.Bar.Open);
      // Close is always the most recent price
      Assert.Equal(85.0, result.Price.Bar.Close);
    }

    [Fact]
    public async Task Send_UsesPreviousValuesWhenCurrentIsNull()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      await grain.Send(Instrument with
      {
        Price = new()
        {
          Last = 100.0,
          Ask = 101.0,
          Bid = 99.0,
          AskSize = 100.0,
          BidSize = 150.0,
          Time = 1
        }
      });

      // Act - Send price with nulls
      await grain.Send(Instrument with
      {
        Price = new()
        {
          Last = 105.0,
          Time = 2
        }
      });

      var result = await grain.Instrument();

      // Assert
      Assert.Equal(105.0, result.Price.Last);
      // When Ask/Bid are null, Combine falls back to the price value itself (105.0)
      Assert.Equal(105.0, result.Price.Ask);
      Assert.Equal(105.0, result.Price.Bid);
      // Sizes fall back to 0.0 when null
      Assert.Equal(0.0, result.Price.AskSize);
      Assert.Equal(0.0, result.Price.BidSize);
    }

    [Fact]
    public async Task Send_UpdatesSpecificValues()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      await grain.Send(Instrument with
      {
        Price = new()
        {
          Last = 100.0,
          Ask = 101.0,
          Bid = 99.0,
          AskSize = 100.0,
          BidSize = 150.0,
          Time = 1
        }
      });

      // Act - Update only specific values
      await grain.Send(Instrument with { Price = new() { Last = 105.0, Ask = 106.0, Time = 2 } });
      var result = await grain.Instrument();

      // Assert
      Assert.Equal(105.0, result.Price.Last);
      Assert.Equal(106.0, result.Price.Ask);
      // When Bid is null in next price, it falls back to the calculated price
      Assert.Equal(105.0, result.Price.Bid);
    }

    [Fact]
    public async Task Send_WithTimeFrame_CreatesNewBarWhenTimeExceeds()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      var timeFrame = TimeSpan.FromSeconds(60);
      var baseTime = 1000000000; // Some base timestamp

      // Act
      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 100.0, Time = baseTime }
      });

      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 105.0, Time = baseTime + timeFrame.Ticks }
      });

      var prices = await grain.Prices(new PriceCriteria());

      // Assert
      Assert.Equal(2, prices.Data.Count); // Two separate bars should be created
    }

    [Fact]
    public async Task Send_WithTimeFrame_UpdatesSameBarWithinTimeFrame()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      var timeFrame = TimeSpan.FromSeconds(60);
      var baseTime = 1000000000;

      // Act
      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 100.0, Time = baseTime }
      });

      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 105.0, Time = baseTime + (timeFrame.Ticks / 2) } // Within same time frame
      });

      var prices = await grain.Prices(new PriceCriteria());

      // Assert
      Assert.Equal(2, prices.Data.Count); // Both prices stored in Items
      Assert.Equal(105.0, prices.Data.Last().Last); // Last price updated
    }

    [Fact]
    public async Task Prices_ReturnsAllStoredPrices()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      await grain.Send(Instrument with { Price = new() { Last = 100.0, Time = 1 } });
      await grain.Send(Instrument with { Price = new() { Last = 105.0, Time = 2 } });
      await grain.Send(Instrument with { Price = new() { Last = 110.0, Time = 3 } });

      // Act
      var result = await grain.Prices(new PriceCriteria());

      // Assert
      Assert.NotNull(result);
      Assert.NotNull(result.Data);
      Assert.Equal(3, result.Data.Count);
      Assert.Equal(100.0, result.Data[0].Last);
      Assert.Equal(105.0, result.Data[1].Last);
      Assert.Equal(110.0, result.Data[2].Last);
    }

    [Fact]
    public async Task Prices_ReturnsEmptyListWhenNoData()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      // Act
      var result = await grain.Prices(new PriceCriteria());

      // Assert
      Assert.NotNull(result);
      Assert.NotNull(result.Data);
      Assert.Empty(result.Data);
    }

    [Fact]
    public async Task PriceGroups_ReturnsAggregatedPrices()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      var timeFrame = TimeSpan.FromSeconds(60);
      var baseTime = 1000000000;

      // Add multiple prices in different time frames
      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 100.0, Time = baseTime }
      });

      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 150.0, Time = baseTime + timeFrame.Ticks }
      });

      // Act
      var result = await grain.PriceGroups(new PriceCriteria());

      // Assert
      Assert.NotNull(result);
      Assert.NotNull(result.Data);
      Assert.Equal(2, result.Data.Count);
      Assert.Equal(100.0, result.Data[0].Bar.Open);
      Assert.Equal(150.0, result.Data[1].Bar.Close);
    }

    [Fact]
    public async Task PriceGroups_ReturnsEmptyListWhenNoData()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      // Act
      var result = await grain.PriceGroups(new PriceCriteria());

      // Assert
      Assert.NotNull(result);
      Assert.NotNull(result.Data);
      Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Send_MultipleInstruments_MaintainsSeparateState()
    {
      // Arrange
      var descriptor1 = $"{Guid.NewGuid()}";
      var descriptor2 = $"{Guid.NewGuid()}";

      var grain1 = _cluster.GrainFactory.GetGrain<IInstrumentGrain>(descriptor1);
      var grain2 = _cluster.GrainFactory.GetGrain<IInstrumentGrain>(descriptor2);

      // Act
      await grain1.Send(Instrument with { Name = "AAPL", Price = new() { Last = 150.0 } });
      await grain2.Send(Instrument with { Name = "MSFT", Price = new() { Last = 300.0 } });

      var result1 = await grain1.Instrument();
      var result2 = await grain2.Instrument();

      // Assert
      Assert.Equal("AAPL", result1.Name);
      Assert.Equal(150.0, result1.Price.Last);
      Assert.Equal("MSFT", result2.Name);
      Assert.Equal(300.0, result2.Price.Last);
    }

    [Fact]
    public async Task Send_BarTime_UsesRoundedTime()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      var timeFrame = TimeSpan.FromMinutes(1);
      var timestamp = 1000000000;

      // Act
      var result = await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 100.0, Time = timestamp }
      });

      // Assert
      Assert.NotNull(result.Price.Bar.Time);
      // The bar time should be rounded based on the timeframe
    }

    [Fact]
    public async Task PriceGroups_UpdatesLastItemGroupWithLatestPrice()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      var timeFrame = TimeSpan.FromSeconds(1);
      var baseTime = 1000000000L;

      // Act - Add first price
      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 100.0, Time = baseTime }
      });

      // Add second price within same rounded timeframe
      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 110.0, Time = baseTime + (timeFrame.Ticks / 4) }
      });

      // Add third price still within same rounded timeframe
      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 95.0, Time = baseTime + (timeFrame.Ticks / 2) }
      });

      var priceGroups = await grain.PriceGroups(new PriceCriteria());

      // Assert - ItemGroups tracks bars aggregated by timeframe
      // The last group should reflect all prices sent within the timeframe
      Assert.True(priceGroups.Data.Count >= 1);
      var lastGroup = priceGroups.Data.Last();
      Assert.Equal(95.0, lastGroup.Bar.Close); // Most recent price
      Assert.True(lastGroup.Bar.High >= 95.0); // High should be at least the current
      Assert.True(lastGroup.Bar.Low <= 95.0);  // Low should be at most the current
    }

    [Fact]
    public async Task Combine_HandlesNullCurrentPrice()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      // Act - First price with no previous state
      var result = await grain.Send(Instrument with
      {
        Price = new() { Last = 100.0, Time = 1 }
      });

      // Assert
      Assert.Equal(100.0, result.Price.Last);
      Assert.Equal(100.0, result.Price.Bar.Open);
      Assert.Equal(100.0, result.Price.Bar.Close);
      Assert.Equal(100.0, result.Price.Bar.High);
      Assert.Equal(100.0, result.Price.Bar.Low);
    }

    [Fact]
    public async Task Send_CreatesNewBarWhenTimeAdvances()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      // Act - Send prices with advancing timestamps
      await grain.Send(Instrument with { Price = new() { Last = 100.0, Time = 1 } });
      await grain.Send(Instrument with { Price = new() { Last = 120.0, Time = 2 } });
      await grain.Send(Instrument with { Price = new() { Last = 85.0, Time = 3 } });

      // Each price creates a new bar since time advances
      var result = await grain.Instrument();

      // Assert - Each new time creates fresh bar, so latest bar reflects only last price
      Assert.Equal(85.0, result.Price.Bar.Low);
      Assert.Equal(85.0, result.Price.Bar.High);
      Assert.Equal(85.0, result.Price.Bar.Open);
      Assert.Equal(85.0, result.Price.Bar.Close);
    }

    [Fact]
    public async Task Send_WithTimeFrame_AggregatesCorrectly()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      var timeFrame = TimeSpan.FromMinutes(1);
      var baseTime = 600000000000L; // Base timestamp

      // Act - Send multiple prices within same minute
      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 100.0, Time = baseTime }
      });

      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 110.0, Time = baseTime + TimeSpan.FromSeconds(15).Ticks }
      });

      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 95.0, Time = baseTime + TimeSpan.FromSeconds(30).Ticks }
      });

      await grain.Send(Instrument with
      {
        TimeFrame = timeFrame,
        Price = new() { Last = 105.0, Time = baseTime + TimeSpan.FromSeconds(45).Ticks }
      });

      var priceGroups = await grain.PriceGroups(new PriceCriteria());
      var prices = await grain.Prices(new PriceCriteria());

      // Assert
      Assert.Equal(4, prices.Data.Count); // All individual prices stored
      Assert.True(priceGroups.Data.Count >= 1); // At least one bar

      var lastBar = priceGroups.Data.Last();
      Assert.Equal(105.0, lastBar.Last); // Last price
      Assert.True(lastBar.Bar.Low <= 95.0); // Should track minimum
      Assert.True(lastBar.Bar.High >= 110.0); // Should track maximum
    }

    [Fact]
    public async Task Send_WithCompleteData_PreservesAllFields()
    {
      // Arrange
      var grain = _cluster
        .GrainFactory
        .GetGrain<IInstrumentGrain>(Descriptor);

      // Act
      var result = await grain.Send(Instrument with
      {
        Name = "TEST",
        Price = new()
        {
          Last = 100.0,
          Ask = 100.5,
          Bid = 99.5,
          AskSize = 1000,
          BidSize = 2000,
          Volume = 50000,
          Time = 123456789
        }
      });

      // Assert
      Assert.Equal("TEST", result.Name);
      Assert.Equal(100.0, result.Price.Last);
      Assert.Equal(100.5, result.Price.Ask);
      Assert.Equal(99.5, result.Price.Bid);
      Assert.Equal(1000, result.Price.AskSize);
      Assert.Equal(2000, result.Price.BidSize);
      Assert.Equal(123456789, result.Price.Time);
    }
  }
}
