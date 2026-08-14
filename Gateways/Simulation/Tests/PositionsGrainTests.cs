using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using Core.Tests;
using Moq;
using Orleans;
using Orleans.TestingHost;
using Simulation.Grains;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Tests
{
  public class Positions : IDisposable
  {
    private readonly Mock<IClusterClient> _mockConnector;
    private readonly TestCluster _cluster;

    private string Descriptor => $"{Guid.NewGuid()}";

    public Positions()
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
    public async Task GetStockBalance()
    {
      var descriptor = Descriptor;
      var stamp = DateTime.Now.Ticks;
      var ordersGrain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);
      var order = new Order
      {
        Amount = 5,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new Operation { Instrument = new() { Name = "SPY" } }
      };

      await ordersGrain.Send(order);
      await ordersGrain.Tap(new() { Name = "SPY", Price = new() { Bid = 520, Ask = 530 } });
      await positionsGrain.Tap(new() { Name = "SPY", Price = new() { Bid = 525, Ask = 535 } });

      var positions = await positionsGrain.Positions(new());

      Assert.Equal((525 - 530) * 5, positions.Data.FirstOrDefault().Balance.Current);

      await positionsGrain.Tap(new() { Name = "SPY", Price = new() { Bid = 490, Ask = 500 } });
      positions = await positionsGrain.Positions(new());
      Assert.Equal((490 - 530) * 5, positions.Data.FirstOrDefault().Balance.Current);

      await positionsGrain.Tap(new() { Name = "SPY", Price = new() { Bid = 550, Ask = 570 } });
      positions = await positionsGrain.Positions(new());
      Assert.Equal((550 - 530) * 5, positions.Data.FirstOrDefault().Balance.Current);
    }

    [Fact]
    public async Task GetOptionBalance()
    {
      var descriptor = Descriptor;
      var stamp = DateTime.Now.Ticks;
      var ordersGrain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);
      var order = new Order
      {
        Amount = 10,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new Operation { Instrument = new() { Name = "SPY260720C750", Leverage = 100, Basis = new() { Name = "SPY" } } }
      };

      await ordersGrain.Send(order);
      await ordersGrain.Tap(new() { Name = "SPY260720C750", Price = new() { Bid = 1.05, Ask = 1.10 } });
      await positionsGrain.Tap(new() { Name = "SPY260720C750", Price = new() { Bid = 1.15, Ask = 1.20 } });

      var positions = await positionsGrain.Positions(new());

      Assert.Equal(Math.Round((1.15 - 1.10) * 100 * 10, 2), Math.Round(positions.Data.FirstOrDefault().Balance.Current.Value, 2));

      await positionsGrain.Tap(new() { Name = "SPY260720C750", Price = new() { Bid = 0.85, Ask = 0.90 } });
      positions = await positionsGrain.Positions(new());
      Assert.Equal(Math.Round((0.85 - 1.10) * 100 * 10, 2), Math.Round(positions.Data.FirstOrDefault().Balance.Current.Value, 2));

      await positionsGrain.Tap(new() { Name = "SPY260720C750", Price = new() { Bid = 2.50, Ask = 2.70 } });
      positions = await positionsGrain.Positions(new());
      Assert.Equal(Math.Round((2.50 - 1.10) * 100 * 10, 2), Math.Round(positions.Data.FirstOrDefault().Balance.Current.Value, 2));
    }

    [Fact]
    public async Task GetFuturesBalance()
    {
      var descriptor = Descriptor;
      var stamp = DateTime.Now.Ticks;
      var ordersGrain = _cluster.GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);
      var order = new Order
      {
        Amount = 10,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new Operation { Instrument = new() { Name = "ES", StepValue = 12.50, StepSize = 0.25, Leverage = 50 } }
      };

      // Long

      await ordersGrain.Send(order);
      await ordersGrain.Tap(new() { Name = "ES", Price = new() { Bid = 7050, Ask = 7100 } });
      await positionsGrain.Tap(new() { Name = "ES", Price = new() { Bid = 7150, Ask = 7200 } });

      var positions = await positionsGrain.Positions(new());

      Assert.Equal(Math.Round((7150.0 - 7100) * 50 * 10, 2), Math.Round(positions.Data.FirstOrDefault().Balance.Current.Value, 2));

      await positionsGrain.Tap(new() { Name = "ES", Price = new() { Bid = 6900, Ask = 6950 } });
      positions = await positionsGrain.Positions(new());
      Assert.Equal(Math.Round((6900.0 - 7100) * 50 * 10, 2), Math.Round(positions.Data.FirstOrDefault().Balance.Current.Value, 2));

      await positionsGrain.Tap(new() { Name = "ES", Price = new() { Bid = 7250, Ask = 7300 } });
      positions = await positionsGrain.Positions(new());
      Assert.Equal(Math.Round((7250.0 - 7100) * 50 * 10, 2), Math.Round(positions.Data.FirstOrDefault().Balance.Current.Value, 2));

      // Long average

      await ordersGrain.Send(order with { Amount = 5 });
      await ordersGrain.Tap(new() { Name = "ES", Price = new() { Bid = 7005, Ask = 7010 } });
      await positionsGrain.Tap(new() { Name = "ES", Price = new() { Bid = 7150, Ask = 7200 } });

      positions = await positionsGrain.Positions(new());

      Assert.Equal(Math.Round((7150.0 - (7100 * 10 + 7010 * 5) / 15) * 50 * 15, 2), Math.Round(positions.Data.FirstOrDefault().Balance.Current.Value, 2));

      // Cleanup

      await ordersGrain.Send(order with { Amount = 15, Side = OrderSideEnum.Short });
      await ordersGrain.Tap(new() { Name = "ES", Price = new() { Bid = 7005, Ask = 7010 } });

      var resOrders = await ordersGrain.Orders(new());
      var resPositions = await positionsGrain.Positions(new());

      Assert.Empty(resOrders.Data);
      Assert.Empty(resPositions.Data);

      // Short

      await ordersGrain.Send(order with { Side = OrderSideEnum.Short });
      await ordersGrain.Tap(new() { Name = "ES", Price = new() { Bid = 7050, Ask = 7100 } });
      await positionsGrain.Tap(new() { Name = "ES", Price = new() { Bid = 7150, Ask = 7200 } });

      positions = await positionsGrain.Positions(new());

      Assert.Equal(Math.Round((7050 - 7200.0) * 50 * 10, 2), Math.Round(positions.Data.FirstOrDefault().Balance.Current.Value, 2));

      await positionsGrain.Tap(new() { Name = "ES", Price = new() { Bid = 6900, Ask = 6950 } });
      positions = await positionsGrain.Positions(new());
      Assert.Equal(Math.Round((7050 - 6950.0) * 50 * 10, 2), Math.Round(positions.Data.FirstOrDefault().Balance.Current.Value, 2));

      await positionsGrain.Tap(new() { Name = "ES", Price = new() { Bid = 7250, Ask = 7300 } });
      positions = await positionsGrain.Positions(new());
      Assert.Equal(Math.Round((7050 - 7300.0) * 50 * 10, 2), Math.Round(positions.Data.FirstOrDefault().Balance.Current.Value, 2));

      // Short average

      await ordersGrain.Send(order with { Amount = 5, Side = OrderSideEnum.Short });
      await ordersGrain.Tap(new() { Name = "ES", Price = new() { Bid = 7005, Ask = 7010 } });
      await positionsGrain.Tap(new() { Name = "ES", Price = new() { Bid = 7150, Ask = 7200 } });

      positions = await positionsGrain.Positions(new());

      Assert.Equal(Math.Round(((7050 * 10 + 7005 * 5) / 15 - 7200.0) * 50 * 15, 2), Math.Round(positions.Data.FirstOrDefault().Balance.Current.Value, 2));
    }

    [Fact]
    public async Task Store()
    {
      var descriptor = Descriptor;
      var stamp = DateTime.Now.Ticks;
      var grain = _cluster.GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);
      var actionsGrain = _cluster.GrainFactory.GetGrain<ITransactionsGrain>(descriptor);
      var instrument = new Instrument
      {
        Name = "SPY",
        Price = new Price
        {
          Bid = 10,
          Ask = 15,
          Last = 25,
          Volume = 1000,
          Time = stamp
        }
      };

      var orderLong = new Order
      {
        Amount = 1,
        Price = 20,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new()
        {
          Amount = 1,
          Time = stamp,
          Status = OrderStatusEnum.Position,
          AveragePrice = instrument.Price.Ask,
          Instrument = instrument
        }
      };

      var orderShort = new Order
      {
        Amount = 1,
        Price = 20,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Operation = new()
        {
          Amount = 1,
          Time = stamp,
          Status = OrderStatusEnum.Position,
          AveragePrice = instrument.Price.Bid,
          Instrument = instrument
        }
      };

      var observer = new Mock<ITradeObserver>();
      var observerReference = _cluster.Client.CreateObjectReference<ITradeObserver>(observer.Object);

      observer
        .Setup(o => o.StreamOrder(It.IsAny<Order>()))
        .Verifiable();

      await actionsGrain.Setup(observerReference);

      // Open

      var openPosition = await grain.Send(orderLong);
      var openPositions = await grain.Positions(default);

      Assert.Single(openPositions.Data);
      Assert.Null(openPosition.Transaction);
      Assert.Equal(JsonSerializer.Serialize(orderLong), JsonSerializer.Serialize(openPositions.Data.First()));
      Assert.Equal(JsonSerializer.Serialize(orderLong), JsonSerializer.Serialize(openPosition.Data));

      // Average down

      var averageDownPosition = await grain.Send(orderLong with { Price = 10 });
      var averageDownPositions = await grain.Positions(default);
      var averageDownExpectation = new Order
      {
        Amount = 2,
        Price = 20,
        Id = orderLong.Id,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new()
        {
          Amount = 2,
          Time = stamp,
          Instrument = instrument,
          Status = OrderStatusEnum.Position,
          AveragePrice = AveragePrice(
            openPosition.Data.Operation.AveragePrice,
            openPosition.Data.Operation.Amount,
            instrument.Price.Ask, 1)
        }
      };

      Assert.Single(averageDownPositions.Data);
      Assert.Null(averageDownPosition.Transaction);
      Assert.Equal(JsonSerializer.Serialize(averageDownExpectation), JsonSerializer.Serialize(averageDownPositions.Data.First()));
      Assert.Equal(JsonSerializer.Serialize(averageDownExpectation), JsonSerializer.Serialize(averageDownPosition.Data));

      // Average up

      var averageUpPosition = await grain.Send(orderLong with { Price = 30 });
      var averageUpPositions = await grain.Positions(default);
      var averageUpExpectation = new Order
      {
        Amount = 3,
        Price = 20,
        Id = orderLong.Id,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new()
        {
          Amount = 3,
          Time = stamp,
          Instrument = instrument,
          Status = OrderStatusEnum.Position,
          AveragePrice = AveragePrice(
            averageDownPosition.Data.Operation.AveragePrice,
            averageDownPosition.Data.Operation.Amount,
            instrument.Price.Ask, 1)
        }
      };

      Assert.Single(averageUpPositions.Data);
      Assert.Null(averageUpPosition.Transaction);
      Assert.Equal(JsonSerializer.Serialize(averageUpExpectation), JsonSerializer.Serialize(averageUpPositions.Data.First()));
      Assert.Equal(JsonSerializer.Serialize(averageUpExpectation), JsonSerializer.Serialize(averageUpPosition.Data));

      // Decrease

      var decreasePosition = await grain.Send(orderShort with { Price = 40 });
      var decreasePositions = await grain.Positions(default);
      var decreaseExpectation = new Order
      {
        Amount = 2,
        Price = 20,
        Id = orderLong.Id,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new()
        {
          Amount = 2,
          Time = stamp,
          Instrument = instrument,
          Status = OrderStatusEnum.Position,
          AveragePrice = averageUpPosition.Data.Operation.AveragePrice
        }
      };

      var decreaseTransaction = new Order
      {
        Amount = 1,
        Price = 20,
        Id = orderLong.Id,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new()
        {
          Amount = 1,
          Time = stamp,
          Instrument = instrument,
          Price = instrument.Price.Bid,
          Status = OrderStatusEnum.Transaction,
          AveragePrice = averageUpPosition.Data.Operation.AveragePrice
        }
      };

      Assert.Single(decreasePositions.Data);
      Assert.Equal(JsonSerializer.Serialize(decreaseExpectation), JsonSerializer.Serialize(decreasePositions.Data.First()));
      Assert.Equal(JsonSerializer.Serialize(decreaseExpectation), JsonSerializer.Serialize(decreasePosition.Data));
      Assert.Equal(JsonSerializer.Serialize(decreaseTransaction), JsonSerializer.Serialize(decreasePosition.Transaction));

      // Inverse

      var inversePosition = await grain.Send(orderShort with { Amount = 3, Price = 50 });
      var inversePositions = await grain.Positions(default);
      var inverseExpectation = new Order
      {
        Amount = 1,
        Price = 50,
        Id = orderShort.Id,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Operation = new()
        {
          Amount = 1,
          Time = stamp,
          Instrument = instrument,
          Status = OrderStatusEnum.Position,
          AveragePrice = instrument.Price.Bid
        }
      };

      var inverseTransaction = new Order
      {
        Amount = 2,
        Price = 20,
        Id = orderLong.Id,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new()
        {
          Amount = 2,
          Time = stamp,
          Instrument = instrument,
          Price = instrument.Price.Bid,
          Status = OrderStatusEnum.Transaction,
          AveragePrice = decreasePosition.Data.Operation.AveragePrice
        }
      };

      Assert.Single(decreasePositions.Data);
      Assert.Equal(JsonSerializer.Serialize(inverseExpectation), JsonSerializer.Serialize(inversePositions.Data.First()));
      Assert.Equal(JsonSerializer.Serialize(inverseExpectation), JsonSerializer.Serialize(inversePosition.Data));
      Assert.Equal(JsonSerializer.Serialize(inverseTransaction), JsonSerializer.Serialize(inversePosition.Transaction));

      // Average short

      var x = await grain.Positions(default);

      var averageShortPosition = await grain.Send(orderShort with { Price = 40 });
      var averageShortPositions = await grain.Positions(default);
      var averageShortExpectation = new Order
      {
        Amount = 2,
        Price = 50,
        Id = orderShort.Id,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Operation = new()
        {
          Amount = 2,
          Time = stamp,
          Instrument = instrument,
          Status = OrderStatusEnum.Position,
          AveragePrice = AveragePrice(
            inversePosition.Data.Operation.AveragePrice,
            inversePosition.Data.Operation.Amount,
            instrument.Price.Bid, 1)
        }
      };

      Assert.Single(averageShortPositions.Data);
      Assert.Equal(JsonSerializer.Serialize(averageShortExpectation), JsonSerializer.Serialize(averageShortPositions.Data.First()));
      Assert.Equal(JsonSerializer.Serialize(averageShortExpectation), JsonSerializer.Serialize(averageShortPosition.Data));

      // Close

      var closePosition = await grain.Send(orderLong with { Amount = 2, Price = 5 });
      var closePositions = await grain.Positions(default);
      var closeTransaction = new Order
      {
        Amount = 2,
        Price = 50,
        Id = orderShort.Id,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Operation = new()
        {
          Amount = 2,
          Time = stamp,
          Instrument = instrument,
          Price = instrument.Price.Ask,
          Status = OrderStatusEnum.Transaction,
          AveragePrice = averageShortExpectation.Operation.AveragePrice
        }
      };

      Assert.Empty(closePositions.Data);
      Assert.Null(closePosition.Data);
      Assert.Equal(JsonSerializer.Serialize(closeTransaction), JsonSerializer.Serialize(closePosition.Transaction));
    }

    private double AveragePrice(double? currentPrice, double? currentAmount, double? price, double? amount)
    {
      return ((currentPrice * currentAmount + price * amount) / (currentAmount + amount)).Value;
    }
  }
}
