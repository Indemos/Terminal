using Simulation;
using System;
using System.Linq;
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
        InitialBalance = 50000,
        Balance = 50000
      };
    }

    /// <summary>
    /// Send various pending orders
    /// </summary>
    /// <param name="orderSide"></param>
    /// <param name="orderType"></param>
    /// <param name="price"></param>
    /// <param name="activationPrice"></param>
    /// <param name="orderPrice"></param>
    /// <returns></returns>
    [Theory]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Stop, 15.0, null, 25.0)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Stop, 15.0, null, 5.0)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Limit, 15.0, null, 5.0)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Limit, 15.0, null, 25.0)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0)]
    public async Task SendPendingOrders(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? price,
      double? activationPrice,
      double? orderPrice)
    {
      var bid = price - 5;
      var ask = price + 5;
      var point = new PointModel()
      {
        Bid = bid,
        Ask = ask,
        Last = price,
        Time = DateTime.Now,
      };

      var instrument = new InstrumentModel()
      {
        Name = "X",
        Point = point,
      };

      var order = new OrderModel
      {
        Id = "Id",
        Descriptor = "Descriptor",
        Amount = 1,
        Side = orderSide,
        Type = orderType,
        Price = orderPrice,
        ActivationPrice = activationPrice,
        TimeSpan = OrderTimeSpanEnum.Ioc,
        Instruction = InstructionEnum.Side,
        Transaction = new() { Instrument = instrument }
      };

      await base.SendOrder(order);

      Assert.Empty(Account.Deals);
      Assert.Single(Account.Orders);
      Assert.Empty(Account.Positions);

      var position = Account.Orders[order.Id];

      Assert.Equal("X", position.Name);
      Assert.Equal("Id", position.Id);
      Assert.Equal("Descriptor", position.Descriptor);
      Assert.Equal(1, position.Amount);
      Assert.Equal(orderSide, position.Side);
      Assert.Equal(orderType, position.Type);
      Assert.Equal(orderPrice, position.Price);
      Assert.Equal(activationPrice, position.ActivationPrice);
      Assert.Equal(OrderTimeSpanEnum.Ioc, position.TimeSpan);
      Assert.Equal(InstructionEnum.Side, position.Instruction);

      Assert.Equal("Id", position.Transaction.Id);
      Assert.Equal(OrderStatusEnum.Pending, position.Transaction.Status);
      Assert.Equal(point.Time, position.Transaction.Time);
      Assert.Null(position.Transaction.AveragePrice);
      Assert.Null(position.Transaction.Amount);
    }

    /// <summary>
    /// Move price up and down to trigger stop and limit orders
    /// </summary>
    /// <param name="orderSide"></param>
    /// <param name="price"></param>
    /// <returns></returns>
    [Theory]
    [InlineData(OrderSideEnum.Long, 500.0)]
    [InlineData(OrderSideEnum.Short, 500.0)]
    public async Task UpdatePositionWithOrders(OrderSideEnum orderSide, double? price)
    {
      var balance = Account.Balance;
      var inverseSide = orderSide is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long;
      var point = new PointModel()
      {
        Bid = price - 10,
        Ask = price + 10,
        Last = price,
        Time = DateTime.Now,
      };

      var instrument = new InstrumentModel()
      {
        Name = "X",
        Point = point,
      };

      var stopUp = new OrderModel
      {
        Amount = 1,
        Price = price + 20,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Stop,
        Transaction = new() { Instrument = instrument }
      };

      var stopDown = new OrderModel
      {
        Amount = 1,
        Price = price - 20,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Stop,
        Transaction = new() { Instrument = instrument }
      };

      var limitUp = new OrderModel
      {
        Amount = 1,
        Price = price + 40,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Limit,
        Transaction = new() { Instrument = instrument }
      };

      var limitDown = new OrderModel
      {
        Amount = 1,
        Price = price - 40,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Limit,
        Transaction = new() { Instrument = instrument }
      };

      var order = new OrderModel
      {
        Amount = 1,
        Side = orderSide,
        Type = OrderTypeEnum.Market,
        Orders = [stopUp, stopDown, limitUp, limitDown],
        Transaction = new() { Instrument = instrument }
      };

      await base.SendOrder(order);

      Assert.Empty(Account.Deals);
      Assert.Single(Account.Positions);
      Assert.Equal(4, Account.Orders.Count);
      Assert.Equal(50000, Account.Balance);

      // Trigger protection

      var stopClosePoint = new PointModel
      {
        Bid = price - 30 * order.GetSide(),
        Ask = price - 30 * order.GetSide(),
        Last = price - 30 * order.GetSide(),
        Time = DateTime.Now,
      };

      instrument.Point = stopClosePoint;

      base.OnPoint(null);

      var stopCloseChange = 0.0;

      switch (true)
      {
        case true when orderSide is OrderSideEnum.Long: stopCloseChange = (stopDown.Price - point.Ask).Value; break;
        case true when orderSide is OrderSideEnum.Short: stopCloseChange = (point.Bid - stopUp.Price).Value; break;
      }

      Assert.Single(Account.Deals);
      Assert.Empty(Account.Positions);
      Assert.Equal(3, Account.Orders.Count);
      Assert.Equal(Account.Balance, balance + stopCloseChange);

      // Trigger limit increase

      var limitIncreasePoint = new PointModel
      {
        Bid = price - 50 * order.GetSide(),
        Ask = price - 50 * order.GetSide(),
        Last = price - 50 * order.GetSide(),
        Time = DateTime.Now
      };

      instrument.Point = limitIncreasePoint;

      base.OnPoint(null);

      Assert.Single(Account.Deals);
      Assert.Single(Account.Positions);
      Assert.Equal(2, Account.Orders.Count);
      Assert.Equal(Account.Balance, balance + stopCloseChange);

      // Trigger stop increase

      var stopIncreasePoint = new PointModel
      {
        Bid = price + 30 * order.GetSide(),
        Ask = price + 30 * order.GetSide(),
        Last = price + 30 * order.GetSide(),
        Time = DateTime.Now,
      };

      instrument.Point = stopIncreasePoint;

      base.OnPoint(null);

      Assert.Single(Account.Deals);
      Assert.Single(Account.Orders);
      Assert.Single(Account.Positions);
      Assert.Equal(Account.Balance, balance + stopCloseChange);

      // Trigger limit close

      var limitClosePoint = new PointModel
      {
        Bid = price + 50 * order.GetSide(),
        Ask = price + 50 * order.GetSide(),
        Last = price + 50 * order.GetSide(),
        Time = DateTime.Now,
      };

      instrument.Point = limitClosePoint;

      base.OnPoint(null);

      var limitCloseChange = 0.0;

      switch (true)
      {
        case true when orderSide is OrderSideEnum.Long: limitCloseChange = (limitUp.Price - (limitDown.Price + stopUp.Price) / 2.0).Value; break;
        case true when orderSide is OrderSideEnum.Short: limitCloseChange = ((limitUp.Price + stopDown.Price) / 2.0 - limitDown.Price).Value; break;
      }

      Assert.Equal(2, Account.Deals.Count);
      Assert.Single(Account.Positions);
      Assert.Empty(Account.Orders);
      Assert.Equal(Account.Balance, balance + stopCloseChange + limitCloseChange);
    }

    /// <summary>
    /// Send simple market orders without suborders
    /// </summary>
    /// <param name="orderSide"></param>
    /// <param name="orderType"></param>
    /// <param name="bid"></param>
    /// <param name="ask"></param>
    /// <param name="price"></param>
    /// <returns></returns>
    [Theory]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Market, 10.0, 20.0, 15.0)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Market, 10.0, 20.0, 15.0)]
    public async Task OpenPositions(
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
        Last = price,
        Time = DateTime.Now,
      };

      var instrument = new InstrumentModel()
      {
        Name = "X",
        Point = point,
      };

      var order = new OrderModel
      {
        Id = "Id",
        Descriptor = "Descriptor",
        Amount = 1,
        Side = orderSide,
        Type = orderType,
        TimeSpan = OrderTimeSpanEnum.Ioc,
        Instruction = InstructionEnum.Side,
        Transaction = new() { Instrument = instrument }
      };

      await base.SendOrder(order);

      var position = Account.Positions[order.Name];
      var openPrice = position.Side is OrderSideEnum.Long ? point.Ask : point.Bid;

      Assert.Empty(Account.Deals);
      Assert.Empty(Account.Orders);
      Assert.Single(Account.Positions);

      Assert.Equal("X", position.Name);
      Assert.Equal("Id", position.Id);
      Assert.Equal("Descriptor", position.Descriptor);
      Assert.Equal(1, position.Amount);
      Assert.Equal(orderSide, position.Side);
      Assert.Equal(OrderTypeEnum.Market, position.Type);
      Assert.Equal(OrderTimeSpanEnum.Ioc, position.TimeSpan);
      Assert.Equal(InstructionEnum.Side, position.Instruction);
      Assert.Equal(openPrice, position.Price);

      Assert.Equal("Id", position.Transaction.Id);
      Assert.Equal(OrderStatusEnum.Filled, position.Transaction.Status);
      Assert.Equal(point.Time, position.Transaction.Time);
      Assert.Equal(openPrice, position.Transaction.AveragePrice);
      Assert.Equal(1, position.Transaction.Amount);
    }

    /// <summary>
    /// Send combinations of orders
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task OpenCombinations()
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

    /// <summary>
    /// Update combined position of orders
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task UpdatePositions()
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
      Assert.Equal(increaseShare.Amount, 150);
      Assert.Equal(increaseShare.Transaction.Amount, increaseShare.Amount);
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
      Assert.Equal(closeShort.Transaction.Amount, closeShort.Amount);
      Assert.Equal(closeShort.Transaction.Status, OrderStatusEnum.Filled);

      Assert.Equal(decreaseShort.Side, OrderSideEnum.Long);
      Assert.Equal(decreaseShort.Type, OrderTypeEnum.Market);
      Assert.Equal(decreaseShort.Price, optionShort.Point.Ask);
      Assert.Equal(decreaseShort.TimeSpan, OrderTimeSpanEnum.Day);
      Assert.Equal(decreaseShort.Transaction.Time, optionShort.Point.Time);
      Assert.Equal(decreaseShort.Transaction.Amount, 1);
      Assert.Equal(decreaseShort.Transaction.Amount, decreaseShort.Amount);
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

    /// <summary>
    /// Reverse position
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ReversePositions()
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
      Assert.Equal(reverseOrder.Transaction.AveragePrice, instrument.Point.Bid);
      Assert.Equal(reverseOrder.Transaction.Amount, 5);
      Assert.Equal(reverseOrder.Transaction.Amount, reverseOrder.Amount);
      Assert.Equal(reverseOrder.Transaction.Status, OrderStatusEnum.Filled);
      Assert.Equal(closeOrder.Transaction.Price, reverseOrder.Price);
    }

    /// <summary>
    /// Open independent non-overlapping positions
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task OpenSeparatePositions()
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
