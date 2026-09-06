using Core.Groups;
using Core.Models;
using System;
using System.Linq;

namespace Tests
{
  public class Prices
  {
    private Instrument Instrument => new Instrument { Name = "SPY" };

    private static TestTimeGroup CreateGroup(TimeSpan? timeFrame = null) => new()
    {
      TimeFrame = timeFrame ?? TimeSpan.Zero
    };

    [Fact]
    public void StoreSetsCurrentPrice()
    {
      var group = CreateGroup();

      var price = group.Send(Instrument with { Price = new() { Last = 100.0 } }).Price;

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
    public void StoreUsesPreviousPrice()
    {
      var group = CreateGroup();

      group.Send(Instrument with { Price = new() { Last = 100.0, Time = 1 } });

      var price = group.Send(Instrument with { Price = new() { Time = 1 } }).Price;

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
    public void StorePreservesPreviousValues()
    {
      var span = TimeSpan.FromMinutes(1);
      var group = CreateGroup(span);

      group.Send(Instrument with
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

      group.Send(Instrument with { Price = new() { Time = 1, Last = 50 } });
      group.Send(Instrument with { Price = new() { Time = 1, Last = 200 } });
      group.Send(Instrument with { Price = new() { Time = 1, Last = 150 } });

      var price = group.Send(Instrument with { Price = new() { Time = 1, Last = 70 } }).Price;

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
    public void StoreUpdatesPreviousValues()
    {
      var group = CreateGroup();

      group.Send(Instrument with
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

      group.Send(Instrument with { Price = new() { Time = 1, Last = 50 } });
      group.Send(Instrument with { Price = new() { Time = 1, Last = 200 } });
      group.Send(Instrument with { Price = new() { Time = 1, Last = 150 } });

      Assert.Equal(10.0, group.Send(Instrument with { Price = new() { Time = 1, Last = 10 } }).Price.Bar.Low);
      Assert.Equal(250.0, group.Send(Instrument with { Price = new() { Time = 1, Last = 250 } }).Price.Bar.High);
      Assert.Equal(15.0, group.Send(Instrument with { Price = new() { Time = 1, Bid = 15 } }).Price.Bid);
      Assert.Equal(25.0, group.Send(Instrument with { Price = new() { Time = 1, Ask = 25 } }).Price.Ask);
      Assert.Equal(15.0, group.Send(Instrument with { Price = new() { Time = 1, BidSize = 15 } }).Price.BidSize);
      Assert.Equal(25.0, group.Send(Instrument with { Price = new() { Time = 1, AskSize = 25 } }).Price.AskSize);
      Assert.Equal(2, group.Send(Instrument with { Price = new() { Time = 2, Last = 15 } }).Price.Bar.Time);
      Assert.Equal(35, group.Send(Instrument with { Price = new() { Time = 3, Last = 35 } }).Price.Bar.Open);
    }

    [Fact]
    public void StoreException()
    {
      var group = CreateGroup();

      Assert.Throws<NullReferenceException>(() => group.Send(null));
      Assert.Throws<NullReferenceException>(() => group.Send(new()));
    }

    [Fact]
    public void Instrument_ReturnsStoredInstrument()
    {
      var group = CreateGroup();
      var instrument = Instrument with { Price = new() { Last = 150.0, Time = 1 } };

      var result = group.Send(instrument);

      Assert.NotNull(result);
      Assert.Equal("SPY", result.Name);
      Assert.Equal(150.0, result.Price.Last);
    }

    [Fact]
    public void Instrument_ReturnsNullWhenNotInitialized()
    {
      var group = CreateGroup();

      Assert.Empty(group.Items);
    }

    [Fact]
    public void Send_StoresFirstPrice()
    {
      var group = CreateGroup();

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

      var result = group.Send(instrument);

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
    public void Send_AccumulatesBarHighAndLowWithinTimeFrame()
    {
      var group = CreateGroup();

      var resultAfterHigh = group.Send(Instrument with { Price = new() { Last = 100.0, Time = 1 } });
      resultAfterHigh = group.Send(Instrument with { Price = new() { Last = 120.0, Time = 1 } });

      var resultAfterLow = group.Send(Instrument with { Price = new() { Last = 85.0, Time = 1 } });

      Assert.Equal(120.0, resultAfterHigh.Price.Bar.Low);
      Assert.Equal(120.0, resultAfterHigh.Price.Bar.High);

      Assert.Equal(85.0, resultAfterLow.Price.Bar.Low);
      Assert.Equal(85.0, resultAfterLow.Price.Bar.High);
    }

    [Fact]
    public void Send_PreservesOpenPriceAndUpdatesClose()
    {
      var group = CreateGroup();

      var first = group.Send(Instrument with { Price = new() { Last = 100.0, Time = 1 } });

      group.Send(Instrument with { Price = new() { Last = 120.0, Time = 2 } });
      var result = group.Send(Instrument with { Price = new() { Last = 85.0, Time = 3 } });

      Assert.Equal(100.0, first.Price.Bar.Open);
      Assert.Equal(85.0, result.Price.Bar.Close);
    }

    [Fact]
    public void Send_UsesPreviousValuesWhenCurrentIsNull()
    {
      var group = CreateGroup();

      group.Send(Instrument with
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

      var result = group.Send(Instrument with
      {
        Price = new()
        {
          Last = 105.0,
          Time = 2
        }
      });

      Assert.Equal(105.0, result.Price.Last);
      Assert.Equal(105.0, result.Price.Ask);
      Assert.Equal(105.0, result.Price.Bid);
      Assert.Equal(0.0, result.Price.AskSize);
      Assert.Equal(0.0, result.Price.BidSize);
    }

    [Fact]
    public void Send_UpdatesSpecificValues()
    {
      var group = CreateGroup();

      group.Send(Instrument with
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

      var result = group.Send(Instrument with { Price = new() { Last = 105.0, Ask = 106.0, Time = 2 } });

      Assert.Equal(105.0, result.Price.Last);
      Assert.Equal(106.0, result.Price.Ask);
      Assert.Equal(105.0, result.Price.Bid);
    }

    [Fact]
    public void Send_WithTimeFrame_CreatesNewBarWhenTimeExceeds()
    {
      var timeFrame = TimeSpan.FromSeconds(60);
      var baseTime = 1000000000;
      var group = CreateGroup(timeFrame);

      group.Send(Instrument with { Price = new() { Last = 100.0, Time = baseTime } });
      group.Send(Instrument with { Price = new() { Last = 105.0, Time = baseTime + timeFrame.Ticks } });

      Assert.Equal(2, group.Items.Count);
    }

    [Fact]
    public void Send_WithTimeFrame_UpdatesSameBarWithinTimeFrame()
    {
      var timeFrame = TimeSpan.FromSeconds(60);
      var baseTime = 1200000000L;
      var group = CreateGroup(timeFrame);

      group.Send(Instrument with { Price = new() { Last = 100.0, Time = baseTime } });
      group.Send(Instrument with { Price = new() { Last = 105.0, Time = baseTime + TimeSpan.FromSeconds(30).Ticks } });

      Assert.Single(group.Items);
      Assert.Equal(105.0, group.Items.Last().Last);
    }

    [Fact]
    public void Prices_ReturnsAllStoredPrices()
    {
      var group = CreateGroup();

      group.Send(Instrument with { Price = new() { Last = 100.0, Time = 1 } });
      group.Send(Instrument with { Price = new() { Last = 105.0, Time = 2 } });
      group.Send(Instrument with { Price = new() { Last = 110.0, Time = 3 } });

      Assert.Equal(3, group.Items.Count);
      Assert.Equal(100.0, group.Items[0].Last);
      Assert.Equal(105.0, group.Items[1].Last);
      Assert.Equal(110.0, group.Items[2].Last);
    }

    [Fact]
    public void Prices_ReturnsEmptyListWhenNoData()
    {
      var group = CreateGroup();

      Assert.Empty(group.Items);
    }

    [Fact]
    public void PriceGroups_ReturnsAggregatedPrices()
    {
      var timeFrame = TimeSpan.FromSeconds(60);
      var baseTime = 1000000000;
      var group = CreateGroup(timeFrame);

      group.Send(Instrument with { Price = new() { Last = 100.0, Time = baseTime } });
      group.Send(Instrument with { Price = new() { Last = 150.0, Time = baseTime + timeFrame.Ticks } });

      Assert.Equal(2, group.Items.Count);
      Assert.Equal(100.0, group.Items[0].Bar.Open);
      Assert.Equal(150.0, group.Items[1].Bar.Close);
    }

    [Fact]
    public void PriceGroups_ReturnsEmptyListWhenNoData()
    {
      var group = CreateGroup(TimeSpan.FromSeconds(60));

      Assert.Empty(group.Items);
    }

    [Fact]
    public void Send_MultipleInstruments_MaintainsSeparateState()
    {
      var group1 = CreateGroup();
      var group2 = CreateGroup();

      var result1 = group1.Send(Instrument with { Name = "AAPL", Price = new() { Last = 150.0 } });
      var result2 = group2.Send(Instrument with { Name = "MSFT", Price = new() { Last = 300.0 } });

      Assert.Equal("AAPL", result1.Name);
      Assert.Equal(150.0, result1.Price.Last);
      Assert.Single(group1.Items);

      Assert.Equal("MSFT", result2.Name);
      Assert.Equal(300.0, result2.Price.Last);
      Assert.Single(group2.Items);
    }

    [Fact]
    public void Send_BarTime_UsesRoundedTime()
    {
      var timeFrame = TimeSpan.FromMinutes(1);
      var timestamp = 1000000000;
      var group = CreateGroup(timeFrame);

      var result = group.Send(Instrument with { Price = new() { Last = 100.0, Time = timestamp } });

      Assert.Equal(timestamp - (timestamp % timeFrame.Ticks), result.Price.Bar.Time);
    }

    [Fact]
    public void PriceGroups_UpdatesLastItemGroupWithLatestPrice()
    {
      var timeFrame = TimeSpan.FromSeconds(1);
      var baseTime = 1000000000L;
      var group = CreateGroup(timeFrame);

      group.Send(Instrument with { Price = new() { Last = 100.0, Time = baseTime } });
      group.Send(Instrument with { Price = new() { Last = 110.0, Time = baseTime + (timeFrame.Ticks / 4) } });
      group.Send(Instrument with { Price = new() { Last = 95.0, Time = baseTime + (timeFrame.Ticks / 2) } });

      Assert.Single(group.Items);
      var lastGroup = group.Items.Last();
      Assert.Equal(95.0, lastGroup.Bar.Close);
      Assert.True(lastGroup.Bar.High >= 95.0);
      Assert.True(lastGroup.Bar.Low <= 95.0);
    }

    [Fact]
    public void Combine_HandlesNullCurrentPrice()
    {
      var group = CreateGroup();

      var result = group.Send(Instrument with { Price = new() { Last = 100.0, Time = 1 } });

      Assert.Equal(100.0, result.Price.Last);
      Assert.Equal(100.0, result.Price.Bar.Open);
      Assert.Equal(100.0, result.Price.Bar.Close);
      Assert.Equal(100.0, result.Price.Bar.High);
      Assert.Equal(100.0, result.Price.Bar.Low);
    }

    [Fact]
    public void Send_CreatesNewBarWhenTimeAdvances()
    {
      var group = CreateGroup();

      group.Send(Instrument with { Price = new() { Last = 100.0, Time = 1 } });
      group.Send(Instrument with { Price = new() { Last = 120.0, Time = 2 } });
      var result = group.Send(Instrument with { Price = new() { Last = 85.0, Time = 3 } });

      Assert.Equal(85.0, result.Price.Bar.Low);
      Assert.Equal(85.0, result.Price.Bar.High);
      Assert.Equal(85.0, result.Price.Bar.Open);
      Assert.Equal(85.0, result.Price.Bar.Close);
    }

    [Fact]
    public void Send_WithTimeFrame_AggregatesCorrectly()
    {
      var timeFrame = TimeSpan.FromMinutes(1);
      var baseTime = 600000000000L;
      var group = CreateGroup(timeFrame);

      group.Send(Instrument with { Price = new() { Last = 100.0, Time = baseTime } });
      group.Send(Instrument with { Price = new() { Last = 110.0, Time = baseTime + TimeSpan.FromSeconds(15).Ticks } });
      group.Send(Instrument with { Price = new() { Last = 95.0, Time = baseTime + TimeSpan.FromSeconds(30).Ticks } });
      group.Send(Instrument with { Price = new() { Last = 105.0, Time = baseTime + TimeSpan.FromSeconds(45).Ticks } });

      Assert.Single(group.Items);

      var lastBar = group.Items.Last();
      Assert.Equal(105.0, lastBar.Last);
      Assert.Equal(95.0, lastBar.Bar.Low);
      Assert.Equal(110.0, lastBar.Bar.High);
      Assert.Equal(100.0, lastBar.Bar.Open);
    }

    [Fact]
    public void Send_WithCompleteData_PreservesAllFields()
    {
      var group = CreateGroup();

      var result = group.Send(Instrument with
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

      Assert.Equal("TEST", result.Name);
      Assert.Equal(100.0, result.Price.Last);
      Assert.Equal(100.5, result.Price.Ask);
      Assert.Equal(99.5, result.Price.Bid);
      Assert.Equal(1000, result.Price.AskSize);
      Assert.Equal(2000, result.Price.BidSize);
      Assert.Equal(123456789, result.Price.Time);
    }

    private sealed class TestTimeGroup : TimeGroup
    {
    }
  }
}
