using Core.Extensions;
using Core.Grains;
using Core.Models;
using Moq;
using Orleans;
using Orleans.TestingHost;
using Simulation.Grains;
using Simulation.Tests;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Simulation.Prices.Tests
{
  public class Prices : IDisposable
  {
    private readonly Mock<IClusterClient> _mockConnector;
    private readonly TestCluster _cluster;

    private string Descriptor => JsonSerializer.Serialize(new DescriptorModel
    {
      Account = $"{Guid.NewGuid()}"
    });

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
    public async Task StoreUsesCurrentPrice()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IPricesGrain>(Descriptor);

      var response = await grain.Store(new PriceModel
      {
        Last = 100.0
      });

      Assert.Null(response.Time);
      Assert.Null(response.TimeFrame);
      Assert.Null(response.Bar.Time);
      Assert.Equal(100.0, response.Last);
      Assert.Equal(100.0, response.Ask);
      Assert.Equal(100.0, response.Bid);
      Assert.Equal(0.0, response.AskSize);
      Assert.Equal(0.0, response.BidSize);
      Assert.Equal(100.0, response.Bar.Low);
      Assert.Equal(100.0, response.Bar.High);
      Assert.Equal(100.0, response.Bar.Open);
      Assert.Equal(100.0, response.Bar.Close);
    }

    [Fact]
    public async Task StoreUsesPreviousPrice()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IPricesGrain>(Descriptor);

      await grain.Store(new PriceModel { Last = 100.0, Time = 1 });

      var response = await grain.Store(new PriceModel { Time = 1 });

      Assert.Null(response.TimeFrame);
      Assert.Equal(1, response.Time);
      Assert.Equal(1, response.Bar.Time);
      Assert.Equal(100.0, response.Last);
      Assert.Equal(100.0, response.Ask);
      Assert.Equal(100.0, response.Bid);
      Assert.Equal(0.0, response.AskSize);
      Assert.Equal(0.0, response.BidSize);
      Assert.Equal(100.0, response.Bar.Low);
      Assert.Equal(100.0, response.Bar.High);
      Assert.Equal(100.0, response.Bar.Open);
      Assert.Equal(100.0, response.Bar.Close);
    }

    [Fact]
    public async Task StorePreservesPreviousValues()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IPricesGrain>(Descriptor);

      await grain.Store(new PriceModel
      {
        Last = 100.0,
        Bid = 90,
        Ask = 110,
        BidSize = 5,
        AskSize = 10,
        Time = 1
      });

      await grain.Store(new PriceModel { Time = 1, Last = 50 });
      await grain.Store(new PriceModel { Time = 1, Last = 200 });
      await grain.Store(new PriceModel { Time = 1, Last = 150 });

      var response = await grain.Store(new PriceModel { Time = 1, Last = 50 });

      Assert.Null(response.TimeFrame);
      Assert.Equal(1, response.Time);
      Assert.Equal(1, response.Bar.Time);
      Assert.Equal(50.0, response.Last);
      Assert.Equal(90.0, response.Bid);
      Assert.Equal(110.0, response.Ask);
      Assert.Equal(5.0, response.BidSize);
      Assert.Equal(10.0, response.AskSize);
      Assert.Equal(50.0, response.Bar.Low);
      Assert.Equal(200.0, response.Bar.High);
      Assert.Equal(100.0, response.Bar.Open);
      Assert.Equal(50.0, response.Bar.Close);
    }

    [Fact]
    public async Task StoreUpdatesPreviousValues()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IPricesGrain>(Descriptor);

      await grain.Store(new PriceModel
      {
        Last = 100.0,
        Bid = 90,
        Ask = 110,
        BidSize = 5,
        AskSize = 10,
        Time = 1
      });

      await grain.Store(new PriceModel { Time = 1, Last = 50 });
      await grain.Store(new PriceModel { Time = 1, Last = 200 });
      await grain.Store(new PriceModel { Time = 1, Last = 150 });

      Assert.Equal(10.0, (await grain.Store(new PriceModel { Time = 1, Last = 10 })).Bar.Low);
      Assert.Equal(250.0, (await grain.Store(new PriceModel { Time = 1, Last = 250 })).Bar.High);
      Assert.Equal(15.0, (await grain.Store(new PriceModel { Time = 1, Bid = 15 })).Bid);
      Assert.Equal(25.0, (await grain.Store(new PriceModel { Time = 1, Ask = 25 })).Ask);
      Assert.Equal(15.0, (await grain.Store(new PriceModel { Time = 1, BidSize = 15 })).BidSize);
      Assert.Equal(25.0, (await grain.Store(new PriceModel { Time = 1, AskSize = 25 })).AskSize);
      Assert.Equal(2, (await grain.Store(new PriceModel { Time = 2, Last = 15 })).Bar.Time);
      Assert.Equal(35, (await grain.Store(new PriceModel { Time = 3, Last = 35 })).Bar.Open);
    }

    [Fact]
    public async Task StoreAggregatesPrices()
    {
      var span = TimeSpan.FromMinutes(1);
      var stamp = new DateTime(2020, 5, 10, 10, 30, 05);
      var price = new PriceModel
      {
        Bid = 150,
        Ask = 200,
        Last = 100,
        BidSize = 50,
        AskSize = 100,
        Time = stamp.Ticks,
        TimeFrame = TimeSpan.FromMinutes(1)
      };

      var grain = _cluster
        .GrainFactory
        .GetGrain<IPricesGrain>(Descriptor);

      await grain.Store(price);
      await grain.Store(price with { Last = 5 });
      await grain.Store(price with { Last = 105 });
      await grain.Store(price with { Last = 50 });

      var sameBar = stamp.AddSeconds(1);

      await grain.Store(price with { Time = sameBar.Ticks });
      await grain.Store(price with { Time = sameBar.Ticks, Last = 15 });
      await grain.Store(price with { Time = sameBar.Ticks, Last = 115 });
      await grain.Store(price with { Time = sameBar.Ticks, Last = 60, Bid = 15, Ask = 20, BidSize = 10, AskSize = 30 });

      var nextBar = stamp.AddSeconds(100);

      await grain.Store(price with { Time = nextBar.Ticks, Last = 155 });
      await grain.Store(price with { Time = nextBar.Ticks, Last = 25 });
      await grain.Store(price with { Time = nextBar.Ticks, Last = 215 });
      await grain.Store(price with { Time = nextBar.Ticks, Last = 160, Bid = 115, Ask = 120 });

      var response = await grain.PriceGroups(default);

      Assert.Equal(price.TimeFrame, response[0].TimeFrame);
      Assert.Equal(sameBar.Ticks, response[0].Time);
      Assert.Equal(sameBar.Round(span).Ticks, response[0].Bar.Time);
      Assert.Equal(15, response[0].Bid);
      Assert.Equal(20, response[0].Ask);
      Assert.Equal(60, response[0].Last);
      Assert.Equal(10, response[0].BidSize);
      Assert.Equal(30, response[0].AskSize);
      Assert.Equal(5, response[0].Bar.Low);
      Assert.Equal(115, response[0].Bar.High);
      Assert.Equal(price.Last, response[0].Bar.Open);
      Assert.Equal(60, response[0].Bar.Close);

      Assert.Equal(price.TimeFrame, response[1].TimeFrame);
      Assert.Equal(nextBar.Ticks, response[1].Time);
      Assert.Equal(nextBar.Round(span).Ticks, response[1].Bar.Time);
      Assert.Equal(115, response[1].Bid);
      Assert.Equal(120, response[1].Ask);
      Assert.Equal(160, response[1].Last);
      Assert.Equal(price.BidSize, response[1].BidSize);
      Assert.Equal(price.AskSize, response[1].AskSize);
      Assert.Equal(25, response[1].Bar.Low);
      Assert.Equal(215, response[1].Bar.High);
      Assert.Equal(155, response[1].Bar.Open);
      Assert.Equal(160, response[1].Bar.Close);
    }

    [Fact]
    public void StoreException()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IPricesGrain>(Descriptor);

      Assert.Throws<AggregateException>(() => grain.Store(null).Result);
      Assert.Throws<AggregateException>(() => grain.Store(new PriceModel()).Result);
    }
  }
}
