using Microsoft.VisualBasic;
using Simulation;
using System;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class Positions : Adapter, IDisposable
  {
    public Positions()
    {
      Account = new Account
      {
        Descriptor = "Demo",
        Balance = 50000
      };
    }

    [Theory]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Stop, 15.0, null, 25.0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 5.0)]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 5.0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Limit, 15.0, null, 25.0)]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0)]
    public void CreatePendingOrder(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? price,
      double? activationPrice,
      double? orderPrice)
    {
      var point = new PointModel()
      {
        Bid = price,
        Ask = price
      };

      var order = new OrderModel
      {
        Side = orderSide,
        Type = orderType,
        ActivationPrice = activationPrice,
        Transaction = new()
        {
          Volume = 1,
          Price = orderPrice,
          Instrument = new InstrumentModel()
          {
            Name = "X",
            Point = point,
            Points = [point]
          }
        }
      };

      var openPrice = Equals(order.Side, OrderSideEnum.Buy) ? point.Bid : point.Ask;

      base.CreateOrders(order);

      Assert.Empty(Account.Orders);
      Assert.Empty(Account.Positions);
      Assert.Single(Account.ActiveOrders);
      Assert.Empty(Account.ActivePositions);

      Assert.Equal(order, Account.ActiveOrders.First());
      Assert.Equal(order.Transaction.Status, OrderStatusEnum.Placed);
    }

    [Theory]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Market, 10.0, 15.0, null)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Market, 10.0, 15.0, 10.0)]
    public void CreateMarketOrder(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? bid,
      double? ask,
      double? price)
    {
      var point = new PointModel()
      {
        Bid = bid,
        Ask = ask
      };

      var instrument = new InstrumentModel()
      {
        Name = "X",
        Point = point,
        Points = [point]
      };

      var order = new OrderModel
      {
        Price = price,
        Side = orderSide,
        Type = orderType,
        Transaction = new()
        {
          Volume = 1,
          Descriptor = "Demo",
          Instrument = instrument
        }
      };

      base.CreateOrders(order);

      var position = Account.ActivePositions.First();
      var openPrice = Equals(position.Order.Side, OrderSideEnum.Buy) ? point.Bid : point.Ask;

      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Single(Account.Orders);
      Assert.Single(Account.ActivePositions);

      Assert.Equal(position.Order.Price, openPrice);
      Assert.Equal(position.Order.Transaction.Price, openPrice);
      Assert.Equal(position.Order.Type, OrderTypeEnum.Market);
      Assert.Equal(position.Order.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.Equal(position.Order.Transaction.Status, OrderStatusEnum.Filled);
      Assert.NotEmpty(position.Order.Transaction.Id);
    }

    [Fact]
    public void CreateMarketOrderWithBrackets()
    {
      var price = 15;
      var point = new PointModel()
      {
        Bid = price,
        Ask = price
      };

      var instrument = new InstrumentModel()
      {
        Name = "X",
        Point = point,
        Points = [point]
      };

      var SL = new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Stop,
        Instruction = InstructionEnum.Brace,
        Transaction = new()
        {
          Volume = 1,
          Price = 5,
          Instrument = instrument
        }
      };

      var TP = new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Limit,
        Instruction = InstructionEnum.Brace,
        Transaction = new()
        {
          Volume = 1,
          Price = 15,
          Instrument = instrument
        }
      };

      var order = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Orders = [SL, TP],
        Transaction = new()
        {
          Volume = 1,
          Price = price,
          Instrument = instrument
        }
      };

      base.CreateOrders(order);

      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Single(Account.Orders);
      Assert.Single(Account.ActivePositions);
    }

    [Fact]
    public void CreateComplexMarketOrder()
    {
      var basis = new InstrumentModel
      {
        Name = "SPY",
        Point = new PointModel { Bid = 545, Ask = 550 }
      };

      var optionLong = new InstrumentModel
      {
        Name = "SPY 240814C00493000",
        Point = new PointModel { Bid = 1.45, Ask = 1.55 },
        Basis = basis
      };

      var optionShort = new InstrumentModel
      {
        Name = "SPY 240814P00493000",
        Point = new PointModel { Bid = 1.15, Ask = 1.25 },
        Basis = basis
      };

      var order = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        TimeSpan = OrderTimeSpanEnum.Day,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 100, Instrument = basis },
          },
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 1, Instrument = optionLong }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 2, Instrument = optionShort }
          }
        ]
      };

      base.CreateOrders(order);

      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Single(Account.Orders);
      Assert.Single(Account.ActivePositions);

      var openOrder = Account.ActivePositions.First().Order;
      var openShare = openOrder.Orders.ElementAt(0);
      var openLong = openOrder.Orders.ElementAt(1);
      var openShort = openOrder.Orders.ElementAt(2);

      Assert.Equal(openShare.Side, OrderSideEnum.Buy);
      Assert.Equal(openShare.Type, OrderTypeEnum.Market);
      Assert.Equal(openShare.Price, basis.Point.Ask);
      Assert.Equal(openShare.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.NotNull(openShare.Transaction.Time);
      Assert.Equal(openShare.Transaction.Volume, 100);
      Assert.Equal(openShare.Transaction.CurrentVolume, openShare.Transaction.Volume);
      Assert.Equal(openShare.Transaction.Status, OrderStatusEnum.Filled);
      Assert.Equal(openShare.Transaction.Price, openShare.Price);

      Assert.Equal(openLong.Side, OrderSideEnum.Buy);
      Assert.Equal(openLong.Type, OrderTypeEnum.Market);
      Assert.Equal(openLong.Price, optionLong.Point.Ask);
      Assert.Equal(openLong.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.NotNull(openLong.Transaction.Time);
      Assert.Equal(openLong.Transaction.Volume, 1);
      Assert.Equal(openLong.Transaction.CurrentVolume, openLong.Transaction.Volume);
      Assert.Equal(openLong.Transaction.Status, OrderStatusEnum.Filled);
      Assert.Equal(openLong.Transaction.Price, openLong.Price);

      Assert.Equal(openShort.Side, OrderSideEnum.Buy);
      Assert.Equal(openShort.Type, OrderTypeEnum.Market);
      Assert.Equal(openShort.Price, optionShort.Point.Ask);
      Assert.Equal(openShort.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.NotNull(openShort.Transaction.Time);
      Assert.Equal(openShort.Transaction.Volume, 2);
      Assert.Equal(openShort.Transaction.CurrentVolume, openShort.Transaction.Volume);
      Assert.Equal(openShort.Transaction.Status, OrderStatusEnum.Filled);
      Assert.Equal(openShort.Transaction.Price, openShort.Price);
    }

    [Fact]
    public void UpdatePosition()
    {
      var basis = new InstrumentModel
      {
        Name = "SPY",
        Point = new PointModel { Bid = 545, Ask = 550 }
      };

      var optionLong = new InstrumentModel
      {
        Name = "SPY 240814C00493000",
        Point = new PointModel { Bid = 1.45, Ask = 1.55 },
        Basis = basis
      };

      var optionShort = new InstrumentModel
      {
        Name = "SPY 240814P00493000",
        Point = new PointModel { Bid = 1.15, Ask = 1.25 },
        Basis = basis
      };

      var order = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        TimeSpan = OrderTimeSpanEnum.Day,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 100, Instrument = basis },
          },
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 1, Instrument = optionLong }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 2, Instrument = optionShort }
          }
        ]
      };

      base.CreateOrders(order);

      // Increase

      var increase = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 50, Instrument = basis },
      };

      base.CreateOrders(increase);

      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Equal(2, Account.Orders.Count);
      Assert.Single(Account.ActivePositions);

      var increaseOrder = Account.ActivePositions.First().Order;
      var increaseShare = increaseOrder.Orders.ElementAt(0);

      Assert.Equal(increaseShare.Side, OrderSideEnum.Buy);
      Assert.Equal(increaseShare.Type, OrderTypeEnum.Market);
      Assert.Equal(increaseShare.Price, basis.Point.Ask);
      Assert.Equal(increaseShare.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.NotNull(increaseShare.Transaction.Time);
      Assert.Equal(increaseShare.Transaction.Volume, 150);
      Assert.Equal(increaseShare.Transaction.CurrentVolume, increaseShare.Transaction.Volume);
      Assert.Equal(increaseShare.Transaction.Status, OrderStatusEnum.Filled);
      Assert.Equal(increaseShare.Transaction.Price, increaseShare.Price);

      // Decrease

      var decrease = new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 1, Instrument = optionShort },
      };

      base.CreateOrders(decrease);

      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Equal(3, Account.Orders.Count);
      Assert.Single(Account.ActivePositions);

      var decreaseOrder = Account.ActivePositions.First().Order;
      var decreaseShort = decreaseOrder.Orders.ElementAt(2);

      Assert.Equal(decreaseShort.Side, OrderSideEnum.Buy);
      Assert.Equal(decreaseShort.Type, OrderTypeEnum.Market);
      Assert.Equal(decreaseShort.Price, optionShort.Point.Ask);
      Assert.Equal(decreaseShort.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.NotNull(decreaseShort.Transaction.Time);
      Assert.Equal(decreaseShort.Transaction.CurrentVolume, 1);
      Assert.NotEqual(decreaseShort.Transaction.CurrentVolume, decreaseShort.Transaction.Volume);
      Assert.Equal(decreaseShort.Transaction.Status, OrderStatusEnum.Filled);
      Assert.Equal(decreaseShort.Transaction.Price, decreaseShort.Price);

      // Reverse 

      var reverse = new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 2, Instrument = optionShort },
      };

      base.CreateOrders(reverse);

      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Equal(4, Account.Orders.Count);
      Assert.Single(Account.ActivePositions);

      var reverseOrder = Account.ActivePositions.First().Order;
      var reverseShort = reverseOrder.Orders.ElementAt(2);

      Assert.Equal(reverseShort.Side, OrderSideEnum.Sell);
      Assert.Equal(reverseShort.Type, OrderTypeEnum.Market);
      Assert.Equal(reverseShort.Price, optionShort.Point.Bid);
      Assert.Equal(reverseShort.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.NotNull(reverseShort.Transaction.Time);
      Assert.Equal(reverseShort.Transaction.CurrentVolume, 1);
      Assert.Equal(reverseShort.Transaction.CurrentVolume, reverseShort.Transaction.Volume);
      Assert.Equal(reverseShort.Transaction.Status, OrderStatusEnum.Filled);
      Assert.Equal(reverseShort.Transaction.Price, reverseShort.Price);
    }
  }
}
