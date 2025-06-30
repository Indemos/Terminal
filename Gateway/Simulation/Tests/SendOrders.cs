using Simulation;
using System;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class SendOrders : Adapter, IDisposable
  {
    public SendOrders()
    {
      Account = new Account
      {
        Descriptor = "Demo",
        Balance = 50000
      };
    }

    [Theory]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Stop, 15.0, null, 25.0)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Stop, 15.0, null, 5.0)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Limit, 15.0, null, 5.0)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Limit, 15.0, null, 25.0)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0)]
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
        Ask = price,
        Last = price,
        Time = DateTime.Now,
      };

      var order = new OrderModel
      {
        Amount = 1,
        Side = orderSide,
        Type = orderType,
        OpenPrice = orderPrice,
        ActivationPrice = activationPrice,
        Name = "X",
      };

      base.SendOrder(order);

      Assert.Empty(Account.Deals);
      Assert.Single(Account.Orders);
      Assert.Empty(Account.Positions);

      var outOrder = Account.Orders[order.Id];

      Assert.NotEmpty(outOrder.Id);
      Assert.Equal(outOrder.Id, order.Id);
      Assert.Equal(outOrder.Type, orderType);
      Assert.Equal(outOrder.OpenPrice, orderPrice);
      Assert.Equal(outOrder.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.Equal(outOrder.Status, OrderStatusEnum.Pending);
    }

    [Theory]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Market, 10.0, 15.0)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Market, 10.0, 15.0)]
    public void CreateMarketOrder(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? bid,
      double? ask)
    {
      var point = new PointModel()
      {
        Bid = bid,
        Ask = ask,
        Last = bid ?? ask,
        Time = DateTime.Now,
      };

      var order = new OrderModel
      {
        Amount = 1,
        Side = orderSide,
        Type = orderType,
        Descriptor = "Demo",
        Name = "X",
      };

      base.SendOrder(order);

      var position = Account.Positions[order.Instrument.Name];
      var openPrice = position.Side is OrderSideEnum.Long ? point.Ask : point.Bid;

      Assert.Empty(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Single(Account.Positions);

      Assert.Equal(position.OpenPrice, openPrice);
      Assert.Equal(position.Instruction, InstructionEnum.Side);
      Assert.Equal(position.Type, OrderTypeEnum.Market);
      Assert.Equal(position.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.Equal(position.Status, OrderStatusEnum.Filled);
      Assert.Equal(position.Id, order.Id);
      Assert.Equal(position.Amount, order.Amount);
      Assert.Equal(position.OpenAmount, order.Amount);
      Assert.Equal(1, position.Amount);
      Assert.NotEmpty(position.Id);
    }

    [Fact]
    public void CreateMarketOrderWithBrackets()
    {
      var price = 155;
      var point = new PointModel()
      {
        Bid = price,
        Ask = price,
        Last = price,
        Time = DateTime.Now,
      };

      var TP = new OrderModel
      {
        Amount = 1,
        OpenPrice = price + 5,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Stop,
        Instruction = InstructionEnum.Brace,
      };

      var SL = new OrderModel
      {
        Amount = 1,
        OpenPrice = price - 5,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Limit,
        Instruction = InstructionEnum.Brace,
      };

      var order = new OrderModel
      {
        Amount = 1,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Orders = [SL, TP],
        Name = "X",
      };

      base.SendOrder(order);

      Assert.Empty(Account.Deals);
      Assert.Equal(2, Account.Orders.Count);
      Assert.Single(Account.Positions);

      // Trigger SL

      base.SetupAccounts();

      var balance = Account.Balance;
      var newPoint = new PointModel
      {
        Bid = point.Bid - 15,
        Ask = point.Ask - 10,
        Last = point.Last - 15,
        Time = DateTime.Now,
      };

      //instrument.Point = newPoint;

      Assert.Equal(balance, 50000);

      base.OnPoint(null);

      Assert.Single(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Empty(Account.Positions);
      Assert.Equal(Account.Balance, balance + (newPoint.Bid - point.Ask));
    }

    [Fact]
    public void CreateMarketOrderWithGroups()
    {
      var basis = new InstrumentModel
      {
        Name = "SPY",
        Point = new PointModel { Bid = 545, Ask = 550, Last = 550, Time = DateTime.Now.AddSeconds(1) }
      };

      var optionShort = new InstrumentModel
      {
        Name = "SPY 240814C00500000",
        Point = new PointModel { Bid = 1.45, Ask = 1.55, Last = 1.55, Time = DateTime.Now.AddSeconds(2) },
        Basis = basis
      };

      var optionLong = new InstrumentModel
      {
        Name = "SPY 240814P00495000",
        Point = new PointModel { Bid = 1.15, Ask = 1.25, Last = 1.25, Time = DateTime.Now.AddSeconds(3) },
        Basis = basis
      };

      var order = new OrderModel
      {
        Id = "1",
        Descriptor = basis.Name,
        Type = OrderTypeEnum.Market,
        TimeSpan = OrderTimeSpanEnum.Gtc,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          new OrderModel
          {
            Amount = 100,
            Name = basis.Name,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
          },
          new OrderModel
          {
            Amount = 5,
            Name = optionLong.Name,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
          },
          new OrderModel
          {
            Amount = 1,
            Name = optionShort.Name,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
          }
        ]
      };

      base.SendOrder(order);

      Assert.Empty(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Equal(3, Account.Positions.Count);

      var openShares = Account.Positions[basis.Name];
      var openLong = Account.Positions[optionLong.Name];
      var openShort = Account.Positions[optionShort.Name];

      Assert.Equal(openShares.Side, OrderSideEnum.Long);
      Assert.Equal(openShares.Type, OrderTypeEnum.Market);
      Assert.Equal(openShares.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.Equal(openShares.OpenPrice, basis.Point.Ask);
      Assert.Equal(openShares.Descriptor, basis.Name);
      Assert.Equal(openShares.Time, basis.Point.Time);
      Assert.Equal(openShares.Amount, 100);
      Assert.Equal(openShares.OpenAmount, openShares.Amount);
      Assert.Equal(openShares.Status, OrderStatusEnum.Filled);
      Assert.Null(openShares.ActivationPrice);
      Assert.Null(openShares.Price);
      Assert.Null(openShares.GainMin);
      Assert.Null(openShares.GainMax);
      Assert.Null(openShares.Gain);

      Assert.Equal(openLong.Side, OrderSideEnum.Long);
      Assert.Equal(openLong.Type, OrderTypeEnum.Market);
      Assert.Equal(openLong.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.Equal(openLong.OpenPrice, optionLong.Point.Ask);
      Assert.Equal(openLong.Descriptor, basis.Name);
      Assert.Equal(openLong.Time, optionLong.Point.Time);
      Assert.Equal(openLong.Amount, 5);
      Assert.Equal(openLong.OpenAmount, openLong.Amount);
      Assert.Equal(openLong.Status, OrderStatusEnum.Filled);
      Assert.Null(openLong.ActivationPrice);
      Assert.Null(openLong.Price);
      Assert.Null(openLong.GainMin);
      Assert.Null(openLong.GainMax);
      Assert.Null(openLong.Gain);

      Assert.Equal(openShort.Side, OrderSideEnum.Short);
      Assert.Equal(openShort.Type, OrderTypeEnum.Market);
      Assert.Equal(openShort.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.Equal(openShort.OpenPrice, optionShort.Point.Bid);
      Assert.Equal(openLong.Descriptor, basis.Name);
      Assert.Equal(openShort.Time, optionShort.Point.Time);
      Assert.Equal(openShort.Amount, 1);
      Assert.Equal(openShort.OpenAmount, openShort.Amount);
      Assert.Equal(openShort.Status, OrderStatusEnum.Filled);
      Assert.Null(openShort.ActivationPrice);
      Assert.Null(openShort.Price);
      Assert.Null(openShort.GainMin);
      Assert.Null(openShort.GainMax);
      Assert.Null(openShort.Gain);
    }

    [Fact]
    public void UpdatePosition()
    {
      var basis = new InstrumentModel
      {
        Name = "SPY",
        Point = new PointModel { Bid = 545, Ask = 550, Last = 550, Time = DateTime.Now, }
      };

      var optionLong = new InstrumentModel
      {
        Name = "SPY 240814C00493000",
        Point = new PointModel { Bid = 1.45, Ask = 1.55, Last = 1.55, Time = DateTime.Now, },
        Basis = basis
      };

      var optionShort = new InstrumentModel
      {
        Name = "SPY 240814P00493000",
        Point = new PointModel { Bid = 1.15, Ask = 1.25, Last = 1.25, Time = DateTime.Now, },
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
            Amount  = 100,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Name = basis.Name,
          },
          new OrderModel
          {
            Amount  = 1,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Name = optionLong.Name
          },
          new OrderModel
          {
            Amount  = 2,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Name = optionShort.Name
          }
        ]
      };

      base.SendOrder(order);

      // Increase

      var increase = new OrderModel
      {
        Amount = 50,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Name = basis.Name,
      };

      base.SendOrder(increase);

      Assert.Empty(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Equal(3, Account.Positions.Count);

      var increaseShare = Account.Positions[basis.Name];

      Assert.Equal(increaseShare.Side, OrderSideEnum.Long);
      Assert.Equal(increaseShare.Type, OrderTypeEnum.Market);
      Assert.Equal(increaseShare.OpenPrice, basis.Point.Ask);
      Assert.Equal(increaseShare.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.Equal(increaseShare.Time, basis.Point.Time);
      Assert.Equal(increaseShare.Amount, 150);
      Assert.Equal(increaseShare.OpenAmount, increaseShare.Amount);
      Assert.Equal(increaseShare.Status, OrderStatusEnum.Filled);
      Assert.Null(increaseShare.Price);

      // Decrease

      var decrease = new OrderModel
      {
        Amount = 1,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Name = optionShort.Name,
      };

      base.SendOrder(decrease);

      Assert.Single(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Equal(3, Account.Positions.Count);

      var decreaseShort = Account.Positions[optionShort.Name];

      Assert.Equal(decreaseShort.Side, OrderSideEnum.Long);
      Assert.Equal(decreaseShort.Type, OrderTypeEnum.Market);
      Assert.Equal(decreaseShort.OpenPrice, optionShort.Point.Ask);
      Assert.Equal(decreaseShort.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.Equal(decreaseShort.Time, optionShort.Point.Time);
      Assert.Equal(decreaseShort.OpenAmount, 1);
      Assert.Equal(decreaseShort.OpenAmount, decreaseShort.Amount);
      Assert.Equal(decreaseShort.Status, OrderStatusEnum.Filled);
      Assert.Null(decreaseShort.Price);

      // Close side

      var close = new OrderModel
      {
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Amount = Account.Positions[basis.Name].Amount,
        Name = basis.Name,
      };

      base.SendOrder(close);

      var closeSide = Account.Deals.Last();

      Assert.Empty(Account.Orders);
      Assert.Equal(2, Account.Deals.Count);
      Assert.Equal(2, Account.Positions.Count);
      Assert.Equal(closeSide.Price, basis.Point.Bid);

      // Close position

      var closePosition = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          new OrderModel
          {
            Amount  = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Name = optionLong.Name
          },
          new OrderModel
          {
            Amount  = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Name = optionShort.Name
          }
        ]
      };

      base.SendOrder(closePosition);

      Assert.Empty(Account.Positions);
      Assert.Empty(Account.Orders);
      Assert.Equal(4, Account.Deals.Count);
    }

    [Fact]
    public void ReversePosition()
    {
      var instrument = new InstrumentModel
      {
        Name = "MSFT",
        Point = new PointModel { Bid = 545, Ask = 550, Last = 550, Time = DateTime.Now, }
      };

      var order = new OrderModel
      {
        Amount = 5,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Name = instrument.Name,
      };

      base.SendOrder(order);

      var reverseOrder = new OrderModel
      {
        Amount = 10,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Name = instrument.Name,
      };

      base.SendOrder(reverseOrder);

      Assert.Single(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Single(Account.Positions);

      var reversePosition = Account.Positions[instrument.Name];

      Assert.Equal(reversePosition.Side, OrderSideEnum.Short);
      Assert.Equal(reversePosition.Type, OrderTypeEnum.Market);
      Assert.Equal(reversePosition.OpenPrice, instrument.Point.Bid);
      Assert.Equal(reversePosition.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.NotNull(reversePosition.Time);
      Assert.Equal(reversePosition.OpenAmount, 5);
      Assert.Equal(reversePosition.OpenAmount, reversePosition.Amount);
      Assert.Equal(reversePosition.Status, OrderStatusEnum.Filled);
      Assert.Equal(reversePosition.Price, reverseOrder.OpenPrice);
    }

    [Fact]
    public void SeparatePosition()
    {
      var orderX = new OrderModel
      {
        Amount = 5,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Name = "SPY",
      };

      var orderY = new OrderModel
      {
        Amount = 5,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Name = "MSFT",
      };

      base.SendOrder(orderX);
      base.SendOrder(orderY);

      Assert.Empty(Account.Orders);
      Assert.Equal(2, Account.Positions.Count);
    }
  }
}
