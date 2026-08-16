using Core.Enums;
using Core.Models;
using Core.Tests;
using Moq;
using Orleans;
using Orleans.TestingHost;
using Simulation.Grains;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Tests
{
  public class Orders : IDisposable
  {
    private readonly Mock<IClusterClient> _mockConnector;
    private readonly TestCluster _cluster;

    private string Descriptor => $"{Guid.NewGuid()}";

    private Instrument Instrument => new()
    {
      Name = "SPY",
      Leverage = 1,
      Commission = 0
    };

    private Price Price(double bid, double ask) => new() { Bid = bid, Ask = ask, Last = ask };

    public Orders()
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

    // Validation 

    [Fact]
    public void SendThrowsOnEmptyOrder()
    {
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(Descriptor);

      Assert.Throws<AggregateException>(() => grain.Send(new Order()).Result);
    }

    // Errors

    [Fact]
    public async Task SendReturnsErrorsOnInvalidOrder()
    {
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(Descriptor);
      var response = await grain.Send(new Order
      {
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument }
      });

      Assert.NotEmpty(response.Errors);

      var orders = (await grain.Orders(default)).Data;
      Assert.Empty(orders);
    }

    // Market orders 

    [Fact]
    public async Task TapFillsMarketLongOrder()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 2,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument }
      });

      await grain.Tap(Instrument with { Price = Price(100, 110) });

      var orders = (await grain.Orders(default)).Data;
      var positions = (await positionsGrain.Positions(default)).Data;

      Assert.Empty(orders);
      Assert.Single(positions);
      Assert.Equal(110, positions.First().Operation.AveragePrice);
      Assert.Equal(2, positions.First().Operation.Amount);
      Assert.Equal(OrderStatusEnum.Position, positions.First().Operation.Status);
    }

    [Fact]
    public async Task TapFillsMarketShortOrder()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 3,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument }
      });

      await grain.Tap(Instrument with { Price = Price(90, 95) });

      var orders = (await grain.Orders(default)).Data;
      var positions = (await positionsGrain.Positions(default)).Data;

      Assert.Empty(orders);
      Assert.Single(positions);
      Assert.Equal(90, positions.First().Operation.AveragePrice);
      Assert.Equal(3, positions.First().Operation.Amount);
    }

    // Limit orders

    [Fact]
    public async Task TapDoesNotFillLimitLongAbovePrice()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 1,
        Price = 100,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Limit,
        Operation = new() { Instrument = Instrument }
      });

      // Ask = 110 > limit 100 — should NOT fill
      await grain.Tap(Instrument with { Price = Price(105, 110) });

      var orders = (await grain.Orders(default)).Data;
      var positions = (await positionsGrain.Positions(default)).Data;

      Assert.Single(orders);
      Assert.Empty(positions);
    }

    [Fact]
    public async Task TapFillsLimitLongAtOrBelowPrice()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 1,
        Price = 100,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Limit,
        Operation = new() { Instrument = Instrument }
      });

      // Ask = 98 <= limit 100 — should fill
      await grain.Tap(Instrument with { Price = Price(95, 98) });

      var orders = (await grain.Orders(default)).Data;
      var positions = (await positionsGrain.Positions(default)).Data;

      Assert.Empty(orders);
      Assert.Single(positions);
      // filled at min(limit, ask) = min(100, 98) = 98
      Assert.Equal(98, positions.First().Operation.AveragePrice);
    }

    [Fact]
    public async Task TapFillsLimitShortAtOrAbovePrice()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 1,
        Price = 105,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Limit,
        Operation = new() { Instrument = Instrument }
      });

      // Bid = 108 >= limit 105 — should fill
      await grain.Tap(Instrument with { Price = Price(108, 112) });

      var orders = (await grain.Orders(default)).Data;
      var positions = (await positionsGrain.Positions(default)).Data;

      Assert.Empty(orders);
      Assert.Single(positions);
      // filled at max(limit, bid) = max(105, 108) = 108
      Assert.Equal(108, positions.First().Operation.AveragePrice);
    }

    // Stop orders

    [Fact]
    public async Task TapDoesNotFillStopLongBelowPrice()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 1,
        Price = 120,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Stop,
        Operation = new() { Instrument = Instrument }
      });

      // Ask = 110 < stop 120 — should NOT fill
      await grain.Tap(Instrument with { Price = Price(105, 110) });

      var positions = (await positionsGrain.Positions(default)).Data;
      Assert.Empty(positions);
    }

    [Fact]
    public async Task TapFillsStopLongAtOrAbovePrice()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 1,
        Price = 120,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Stop,
        Operation = new() { Instrument = Instrument }
      });

      // Ask = 125 >= stop 120 — should fill
      await grain.Tap(Instrument with { Price = Price(122, 125) });

      var positions = (await positionsGrain.Positions(default)).Data;

      Assert.Single(positions);
      Assert.Equal(125, positions.First().Operation.AveragePrice);
    }

    [Fact]
    public async Task TapFillsStopShortAtOrBelowPrice()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 1,
        Price = 90,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Stop,
        Operation = new() { Instrument = Instrument }
      });

      // Bid = 85 <= stop 90 — should fill
      await grain.Tap(Instrument with { Price = Price(85, 88) });

      var positions = (await positionsGrain.Positions(default)).Data;

      Assert.Single(positions);
      Assert.Equal(85, positions.First().Operation.AveragePrice);
    }

    // Stop Limit orders

    [Fact]
    public async Task TapFillsStopLimitLongWhenActivatedAndBelowLimit()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 1,
        Price = 115,          // limit price
        ActivationPrice = 110, // stop trigger
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.StopLimit,
        Operation = new() { Instrument = Instrument }
      });

      // First tick: Ask=112 >= activation 110 — does not activate as limit, Ask > limit 115 — stays open
      await grain.Tap(Instrument with { Price = Price(105, 112) });
      Assert.Empty((await positionsGrain.Positions(default)).Data);

      // Second tick: Ask=113 <= limit 115 — fills
      await grain.Tap(Instrument with { Price = Price(110, 113) });

      var positions = (await positionsGrain.Positions(default)).Data;
      Assert.Single(positions);
    }

    // Instrument checks

    [Fact]
    public async Task TapIgnoresOrderForDifferentInstrument()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 1,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument }   // SPY
      });

      // Tap with a different instrument
      await grain.Tap(new Instrument { Name = "AAPL", Price = Price(100, 110) });

      var orders = (await grain.Orders(default)).Data;
      var positions = (await positionsGrain.Positions(default)).Data;

      Assert.Single(orders);   // still pending
      Assert.Empty(positions);
    }

    // Multiple orders same tick

    [Fact]
    public async Task TapFillsMultipleOrdersInOneTick()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 1,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument }
      });

      await grain.Send(new Order
      {
        Amount = 2,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument }
      });

      await grain.Tap(Instrument with { Price = Price(100, 105) });

      var orders = (await grain.Orders(default)).Data;
      var positions = (await positionsGrain.Positions(default)).Data;

      Assert.Empty(orders);
      // Both market orders fill and aggregate into one position
      Assert.Single(positions);
      Assert.Equal(3, positions.First().Operation.Amount);
    }

    // Bracket orders

    [Fact]
    public async Task TapRegistersStopLossBraceAfterFill()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);

      var sl = new Order
      {
        Amount = 1,
        Price = 90,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Stop,
        Instruction = InstructionEnum.Brace,
        Operation = new() { Instrument = Instrument }
      };

      await grain.Send(new Order
      {
        Amount = 1,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument },
        Orders = [sl]
      });

      await grain.Tap(Instrument with { Price = Price(100, 105) });

      var orders = (await grain.Orders(default)).Data;

      // Parent market order is gone, brace (SL) should now be registered
      Assert.Single(orders);
      Assert.Equal(InstructionEnum.Brace, orders.First().Instruction);
      Assert.Equal(OrderTypeEnum.Stop, orders.First().Type);
    }

    [Fact]
    public async Task TapFillsStopLossBraceAndClosesPosition()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      var sl = new Order
      {
        Amount = 1,
        Price = 90,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Stop,
        Instruction = InstructionEnum.Brace,
        Operation = new() { Instrument = Instrument }
      };

      // Send and fill entry order → SL gets registered
      await grain.Send(new Order
      {
        Amount = 1,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument },
        Orders = [sl]
      });

      await grain.Tap(Instrument with { Price = Price(100, 105) });

      // SL triggers: Bid = 88 <= stop 90
      await grain.Tap(Instrument with { Price = Price(88, 92) });

      var orders = (await grain.Orders(default)).Data;
      var positions = (await positionsGrain.Positions(default)).Data;

      Assert.Empty(orders);
      Assert.Empty(positions);
    }

    [Fact]
    public async Task TapClosesPositionIncludingOco()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      var sl = new Order
      {
        Amount = 1,
        Price = 90,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Stop,
        Instruction = InstructionEnum.Brace,
        Operation = new() { Instrument = Instrument }
      };

      // Send and fill entry order → SL gets registered
      await grain.Send(new Order
      {
        Amount = 1,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument },
        Orders = [sl]
      });

      await grain.Tap(Instrument with { Price = Price(100, 105) });

      await grain.Send(new Order
      {
        Amount = 1,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument },
        Orders = [sl]
      });

      await grain.Tap(Instrument with { Price = Price(100, 110) });

      var orders = (await grain.Orders(default)).Data;
      var positions = (await positionsGrain.Positions(default)).Data;

      Assert.Empty(orders);
      Assert.Empty(positions);
    }

    // Ordering / state integrity

    [Fact]
    public async Task ClearRemovesOrderFromState()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);

      await grain.Send(new Order
      {
        Amount = 1,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument }
      });

      await grain.Send(new Order
      {
        Amount = 1,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = Instrument }
      });

      var beforeClear = (await grain.Orders(default)).Data;
      Assert.Equal(2, beforeClear.Count);

      await grain.Clear(beforeClear.First());

      var afterClear = (await grain.Orders(default)).Data;
      Assert.Single(afterClear);
    }

    [Fact]
    public async Task TapDoesNotThrowWhenStateIsEmpty()
    {
      var grain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(Descriptor);

      var exception = await Record.ExceptionAsync(() =>
        grain.Tap(Instrument with { Price = Price(100, 110) }));

      Assert.Null(exception);
    }
  }
}
