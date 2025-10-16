using Core.Models;
using Core.Tests;
using Moq;
using Orleans;
using Orleans.TestingHost;
using Simulation.Grains;
using System;
using System.Threading.Tasks;

namespace Simulation.Prices.Tests
{
  public class Prices : IDisposable
  {
    private readonly Mock<IClusterClient> _mockConnector;
    private readonly TestCluster _cluster;

    private string Descriptor => $"{Guid.NewGuid()}";

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
        .GetGrain<IGatewayPricesGrain>(Descriptor);

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
        .GetGrain<IGatewayPricesGrain>(Descriptor);

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
        .GetGrain<IGatewayPricesGrain>(Descriptor);

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
        .GetGrain<IGatewayPricesGrain>(Descriptor);

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
    public void StoreException()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IGatewayPricesGrain>(Descriptor);

      Assert.Throws<AggregateException>(() => grain.Store(null).Result);
      Assert.Throws<AggregateException>(() => grain.Store(new PriceModel()).Result);
    }
  }
}
