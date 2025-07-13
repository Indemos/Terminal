using Simulation;
using System;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
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
    public async Task CreatePendingOrder(
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
        Price = orderPrice,
        ActivationPrice = activationPrice,
        Transaction = new()
        {
          Instrument = new InstrumentModel()
          {
            Name = "X",
            Point = point,
          }
        }
      };

      await base.SendOrder(order);

      Assert.Empty(Account.Deals);
      Assert.Single(Account.Orders);
      Assert.Empty(Account.Positions);

      var outOrder = Account.Orders[order.Id];

      Assert.Equal(outOrder.Type, orderType);
      Assert.Equal(outOrder.Price, orderPrice);
      Assert.Equal(outOrder.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.NotEmpty(outOrder.Id);
      Assert.NotEmpty(outOrder.Transaction.Id);
      Assert.Equal(outOrder.Transaction.Id, order.Id);
      Assert.Equal(outOrder.Transaction.Status, OrderStatusEnum.Pending);
    }

    [Theory]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Market, 10.0, 15.0, null)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Market, 10.0, 15.0, 10.0)]
    public async Task CreateMarketOrder(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? bid,
      double? ask,
      double? price)
    {
      var point = new PointModel()
      {
        Bid = bid,
        Ask = ask,
        Last = bid ?? ask,
        Time = DateTime.Now,
      };

      var instrument = new InstrumentModel()
      {
        Name = "X",
        Point = point,
      };

      var order = new OrderModel
      {
        Amount = 1,
        Price = price,
        Side = orderSide,
        Type = orderType,
        Descriptor = "Demo",
        Transaction = new()
        {
          Instrument = instrument
        }
      };

      await base.SendOrder(order);

      var position = Account.Positions[order.Name];
      var openPrice = position.Side is OrderSideEnum.Long ? point.Ask : point.Bid;

      Assert.Empty(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Single(Account.Positions);

      Assert.Equal(position.Price, openPrice);
      Assert.Equal(position.Transaction.AveragePrice, openPrice);
      Assert.Equal(position.Instruction, InstructionEnum.Side);
      Assert.Null(position.Transaction.Price);
      Assert.Equal(position.Type, OrderTypeEnum.Market);
      Assert.Equal(position.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.Equal(position.Transaction.Status, OrderStatusEnum.Filled);
      Assert.Equal(position.Transaction.Id, order.Id);
      Assert.Equal(position.Amount, order.Amount);
      Assert.Equal(position.Transaction.Amount, order.Amount);
      Assert.NotNull(position.Transaction.Id);
      Assert.NotNull(position.Amount);
    }

    [Fact]
    public async Task CreateMarketOrderWithBrackets()
    {
      var price = 155;
      var point = new PointModel()
      {
        Bid = price,
        Ask = price,
        Last = price,
        Time = DateTime.Now,
      };

      var instrument = new InstrumentModel()
      {
        Name = "X",
        Point = point,
      };

      var TP = new OrderModel
      {
        Amount = 1,
        Price = price + 5,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Stop,
        Instruction = InstructionEnum.Brace,
        Transaction = new()
        {
          Instrument = instrument
        }
      };

      var SL = new OrderModel
      {
        Amount = 1,
        Price = price - 5,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Limit,
        Instruction = InstructionEnum.Brace,
        Transaction = new()
        {
          Instrument = instrument
        }
      };

      var order = new OrderModel
      {
        Amount = 1,
        Price = price,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Orders = [SL, TP],
        Transaction = new()
        {
          Instrument = instrument
        }
      };

      await base.SendOrder(order);

      Assert.Empty(Account.Deals);
      Assert.Equal(2, Account.Orders.Count);
      Assert.Single(Account.Positions);

      // Trigger SL

      Account.InitialBalance = Account.Balance;

      var balance = Account.Balance;
      var newPoint = new PointModel
      {
        Bid = point.Bid - 15,
        Ask = point.Ask - 10,
        Last = point.Last - 15,
        Time = DateTime.Now,
      };

      instrument.Point = newPoint;

      Assert.Equal(balance, 50000);

      base.OnPoint(null);

      Assert.Single(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Empty(Account.Positions);
      Assert.Equal(Account.Balance, balance + (newPoint.Bid - point.Ask));
    }

    [Fact]
    public async Task CreateComplexMarketOrder()
    {
      var basis = new InstrumentModel
      {
        Name = "SPY",
        Point = new PointModel { Bid = 545, Ask = 550, Last = 550 , Time = DateTime.Now }
      };

      var optionLong = new InstrumentModel
      {
        Name = "SPY 240814C00493000",
        Point = new PointModel { Bid = 1.45, Ask = 1.55, Last = 1.55 , Time = DateTime.Now },
        Basis = basis
      };

      var optionShort = new InstrumentModel
      {
        Name = "SPY 240814P00493000",
        Point = new PointModel { Bid = 1.15, Ask = 1.25, Last = 1.25 , Time = DateTime.Now },
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
            Amount = 100,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = basis },
          },
          new OrderModel
          {
            Amount = 1, 
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = optionLong }
          },
          new OrderModel
          {
            Amount = 2,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = optionShort }
          }
        ]
      };

      await base.SendOrder(order);

      Assert.Empty(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Equal(3, Account.Positions.Count);

      var openShare = Account.Positions[basis.Name];
      var openLong = Account.Positions[optionLong.Name];
      var openShort = Account.Positions[optionShort.Name];

      Assert.Equal(openShare.Side, OrderSideEnum.Long);
      Assert.Equal(openShare.Type, OrderTypeEnum.Market);
      Assert.Equal(openShare.Price, basis.Point.Ask);
      Assert.Equal(openShare.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.NotNull(openShare.Transaction.Time);
      Assert.Equal(openShare.Amount, 100);
      Assert.Equal(openShare.Transaction.Amount, openShare.Amount);
      Assert.Equal(openShare.Transaction.Status, OrderStatusEnum.Filled);

      Assert.Equal(openLong.Side, OrderSideEnum.Long);
      Assert.Equal(openLong.Type, OrderTypeEnum.Market);
      Assert.Equal(openLong.Price, optionLong.Point.Ask);
      Assert.Equal(openLong.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.NotNull(openLong.Transaction.Time);
      Assert.Equal(openLong.Amount, 1);
      Assert.Equal(openLong.Transaction.Amount, openLong.Amount);
      Assert.Equal(openLong.Transaction.Status, OrderStatusEnum.Filled);

      Assert.Equal(openShort.Side, OrderSideEnum.Long);
      Assert.Equal(openShort.Type, OrderTypeEnum.Market);
      Assert.Equal(openShort.Price, optionShort.Point.Ask);
      Assert.Equal(openShort.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.NotNull(openShort.Transaction.Time);
      Assert.Equal(openShort.Amount, 2);
      Assert.Equal(openShort.Transaction.Amount, openShort.Amount);
      Assert.Equal(openShort.Transaction.Status, OrderStatusEnum.Filled);
    }

    [Fact]
    public async Task UpdatePosition()
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
            Amount = 100,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = basis },
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = optionLong }
          },
          new OrderModel
          {
            Amount = 2,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = optionShort }
          }
        ]
      };

      await base.SendOrder(order);

      // Increase

      var increase = new OrderModel
      {
        Amount = 50,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Instrument = basis },
      };

      await base.SendOrder(increase);

      Assert.Empty(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Equal(3, Account.Positions.Count);

      var increaseShare = Account.Positions[basis.Name];

      Assert.Equal(increaseShare.Side, OrderSideEnum.Long);
      Assert.Equal(increaseShare.Type, OrderTypeEnum.Market);
      Assert.Equal(increaseShare.Price, basis.Point.Ask);
      Assert.Equal(increaseShare.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.Equal(increaseShare.Transaction.Time, basis.Point.Time);
      Assert.Equal(increaseShare.Amount, 100);
      Assert.Equal(increaseShare.Transaction.Amount, 150);
      Assert.Equal(increaseShare.Transaction.Status, OrderStatusEnum.Filled);

      // Decrease

      var decrease = new OrderModel
      {
        Amount = 1,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Instrument = optionShort },
      };

      await base.SendOrder(decrease);

      Assert.Single(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Equal(3, Account.Positions.Count);

      var closeShort = Account.Deals.Last();
      var decreaseShort = Account.Positions[optionShort.Name];

      Assert.Equal(closeShort.Side, OrderSideEnum.Long);
      Assert.Equal(closeShort.Type, OrderTypeEnum.Market);
      Assert.Equal(closeShort.Price, optionShort.Point.Ask);
      Assert.Equal(closeShort.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.Equal(closeShort.Transaction.Time, optionShort.Point.Time);
      Assert.Equal(closeShort.Transaction.Amount, 1);
      Assert.Equal(closeShort.Amount, 2);
      Assert.Equal(closeShort.Transaction.Status, OrderStatusEnum.Filled);

      Assert.Equal(decreaseShort.Side, OrderSideEnum.Long);
      Assert.Equal(decreaseShort.Type, OrderTypeEnum.Market);
      Assert.Equal(decreaseShort.Price, optionShort.Point.Ask);
      Assert.Equal(decreaseShort.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.Equal(decreaseShort.Transaction.Time, optionShort.Point.Time);
      Assert.Equal(decreaseShort.Transaction.Amount, 1);
      Assert.Equal(decreaseShort.Amount, 2);
      Assert.Equal(decreaseShort.Transaction.Status, OrderStatusEnum.Filled);

      // Close side

      var close = new OrderModel
      {
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Amount = Account.Positions[basis.Name].Transaction.Amount,
        Transaction = new() { Instrument = basis },
      };

      await base.SendOrder(close);

      var closeSide = Account.Deals.Last();

      Assert.Empty(Account.Orders);
      Assert.Equal(2, Account.Deals.Count);
      Assert.Equal(2, Account.Positions.Count);
      Assert.Equal(closeSide.Transaction.Price, basis.Point.Bid);

      // Close position

      var closePosition = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = optionLong }
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = optionShort }
          }
        ]
      };

      await base.SendOrder(closePosition);

      Assert.Empty(Account.Orders);
      Assert.Empty(Account.Positions);
      Assert.Equal(4, Account.Deals.Count);
    }

    [Fact]
    public async Task ReversePosition()
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
        Transaction = new() { Instrument = instrument },
      };

      await base.SendOrder(order);

      var reverse = new OrderModel
      {
        Amount = 10,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Instrument = instrument },
      };

      await base.SendOrder(reverse);

      Assert.Single(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Single(Account.Positions);

      var reverseOrder = Account.Positions[instrument.Name];
      var closeOrder = Account.Deals.Last();

      Assert.Equal(reverseOrder.Side, OrderSideEnum.Short);
      Assert.Equal(reverseOrder.Type, OrderTypeEnum.Market);
      Assert.Equal(reverseOrder.Price, instrument.Point.Bid);
      Assert.Equal(reverseOrder.TimeSpan, OrderTimeSpanEnum.Gtc);
      Assert.NotNull(reverseOrder.Transaction.Time);
      Assert.Equal(reverseOrder.Transaction.Amount, 5);
      Assert.Equal(reverseOrder.Amount, 10);
      Assert.Equal(reverseOrder.Transaction.Status, OrderStatusEnum.Filled);
      Assert.Equal(closeOrder.Transaction.Price, reverseOrder.Price);
    }

    [Fact]
    public async Task SeparatePosition()
    {
      var orderX = new OrderModel
      {
        Amount = 5,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Transaction = new()
        {
          Instrument = new InstrumentModel
          {
            Name = "SPY",
            Point = new PointModel { Bid = 545, Ask = 550, Last = 550, Time = DateTime.Now, }
          }
        },
      };

      var orderY = new OrderModel
      {
        Amount = 5,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Transaction = new()
        {
          Instrument = new InstrumentModel
          {
            Name = "MSFT",
            Point = new PointModel { Bid = 145, Ask = 150, Last = 150 , Time = DateTime.Now }
          }
        },
      };

      await base.SendOrder(orderX);
      await base.SendOrder(orderY);

      Assert.Empty(Account.Orders);
      Assert.Equal(2, Account.Positions.Count);
    }
  }
}
