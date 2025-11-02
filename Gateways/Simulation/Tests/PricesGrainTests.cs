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
    private InstrumentModel Instrument => new InstrumentModel { Name = "SPY" };

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
        .GetGrain<IGatewayInstrumentGrain>(Descriptor);

      var response = await grain.Store(Instrument with { Price = new() { Last = 100.0 } });
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
        .GetGrain<IGatewayInstrumentGrain>(Descriptor);

      await grain.Store(Instrument with { Price = new() { Last = 100.0, Time = 1 } });

      var response = await grain.Store(Instrument with { Price = new() { Time = 1 } });
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
        .GetGrain<IGatewayInstrumentGrain>(Descriptor);

      await grain.Store(Instrument with
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

      await grain.Store(Instrument with { Price = new() { Time = 1, Last = 50 } });
      await grain.Store(Instrument with { Price = new() { Time = 1, Last = 200 }});
      await grain.Store(Instrument with { Price = new() { Time = 1, Last = 150 } });

      var response = await grain.Store(Instrument with { Price = new() { Time = 1, Last = 50 } });
      var price = response.Price;

      Assert.Equal(1, price.Time);
      Assert.Equal(1, price.Bar.Time);
      Assert.Equal(50.0, price.Last);
      Assert.Equal(90.0, price.Bid);
      Assert.Equal(110.0, price.Ask);
      Assert.Equal(5.0, price.BidSize);
      Assert.Equal(10.0, price.AskSize);
      Assert.Equal(50.0, price.Bar.Low);
      Assert.Equal(200.0, price.Bar.High);
      Assert.Equal(100.0, price.Bar.Open);
      Assert.Equal(50.0, price.Bar.Close);
    }

    [Fact]
    public async Task StoreUpdatesPreviousValues()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IGatewayInstrumentGrain>(Descriptor);

      await grain.Store(Instrument with
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

      await grain.Store(Instrument with { Price = new() { Time = 1, Last = 50 } });
      await grain.Store(Instrument with { Price = new() { Time = 1, Last = 200 } });
      await grain.Store(Instrument with { Price = new() { Time = 1, Last = 150 } });

      Assert.Equal(10.0, (await grain.Store(Instrument with { Price = new() { Time = 1, Last = 10 } })).Price.Bar.Low);
      Assert.Equal(250.0, (await grain.Store(Instrument with { Price = new() { Time = 1, Last = 250 } })).Price.Bar.High);
      Assert.Equal(15.0, (await grain.Store(Instrument with { Price = new() { Time = 1, Bid = 15 } })).Price.Bid);
      Assert.Equal(25.0, (await grain.Store(Instrument with { Price = new() { Time = 1, Ask = 25 } })).Price.Ask);
      Assert.Equal(15.0, (await grain.Store(Instrument with { Price = new() { Time = 1, BidSize = 15 } })).Price.BidSize);
      Assert.Equal(25.0, (await grain.Store(Instrument with { Price = new() { Time = 1, AskSize = 25 } })).Price.AskSize);
      Assert.Equal(2, (await grain.Store(Instrument with { Price = new() { Time = 2, Last = 15 } })).Price.Bar.Time);
      Assert.Equal(35, (await grain.Store(Instrument with { Price = new() { Time = 3, Last = 35 } })).Price.Bar.Open);
    }

    [Fact]
    public void StoreException()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IGatewayInstrumentGrain>(Descriptor);

      Assert.Throws<AggregateException>(() => grain.Store(null).Result);
      Assert.Throws<AggregateException>(() => grain.Store(new()).Result);
    }
  }
}
