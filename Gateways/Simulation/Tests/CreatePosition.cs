using System;
using System.Linq;
using Terminal.Connector.Simulation;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class CreatePosition : Adapter, IDisposable
  {
    public CreatePosition()
    {
      Account = new Account
      {
        Name = "Demo",
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
      var order = new OrderModel
      {
        Side = orderSide,
        Type = orderType,
        ActivationPrice = activationPrice,
        Transaction = new()
        {
          Volume = 1,
          Price = orderPrice,
          Instrument = new Instrument()
          {
            Name = "X",
            Points = new ObservableTimeCollection<PointModel>
            {
              new() { Bid = price, Ask = price }
            }
          }
        }
      };

      var orderId = order.Transaction.Id;

      base.SendPendingOrder(order);

      Assert.Equal(order.Transaction.Status, OrderStatusEnum.Placed);
      Assert.Equal(order, Account.Orders[0]);
      Assert.Equal(order, Account.ActiveOrders[orderId]);
      Assert.Single(Account.Orders);
      Assert.Single(Account.ActiveOrders);
      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActivePositions);
    }

    [Theory]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Market, 10.0, 15.0, 15.0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Market, 10.0, 15.0, 10.0)]
    public void CreateMarketOrder(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? bid,
      double? ask,
      double? price)
    {
      var order = new OrderModel
      {
        Side = orderSide,
        Type = orderType,
        Transaction = new()
        {
          Volume = 1,
          Price = price,
          Instrument = new Instrument()
          {
            Name = "X",
            Points = new ObservableTimeCollection<PointModel>
            {
              new() { Bid = bid, Ask = ask }
            }
          }
        }
      };

      var orderId = order.Transaction.Id;

      base.CreatePosition(order);

      var position = Account.ActivePositions[orderId];
      var openPrice = position.Orders.First();
      var closePrice = position.Orders.Last();

      Assert.Equal(position.Order.Transaction.Price, price);
      Assert.Equal(position.Order.Transaction.Status, OrderStatusEnum.Filled);
      Assert.Equal(position.Order.Transaction.Time, order.Transaction.Time);
      Assert.Equal(position.Order.Transaction.Price, price);
      Assert.Equal(openPrice.Transaction.Price, price);
      Assert.Equal(closePrice.Transaction.Price, price);
      Assert.Single(position.Orders);

      Assert.Equal(orderId, Account.Orders[0].Transaction.Id);
      Assert.Equal(orderId, Account.ActivePositions[orderId].Order.Transaction.Id);
      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Single(Account.Orders);
      Assert.Single(Account.ActivePositions);
    }

    [Fact]
    public void CreateMarketOrderWithBrackets()
    {
      var price = 15;
      var instrument = new Instrument()
      {
        Name = "X",
        Points = new ObservableTimeCollection<PointModel>
        {
          new() { Bid = price, Ask = price }
        }
      };

      var order = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type =  OrderTypeEnum.Market,
        Transaction = new()
        {
          Volume = 1,
          Price = price,
          Instrument = instrument
        }
      };

      var SL = new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Stop,
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
        Transaction = new()
        {
          Volume = 1,
          Price = 15,
          Instrument = instrument
        }
      };

      var orderId = order.Transaction.Id;

      order.Orders.Add(SL);
      order.Orders.Add(TP);

      var position = base.CreatePosition(order);

      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Single(Account.Orders);
      Assert.Single(Account.ActivePositions);
      Assert.Equal(orderId, Account.Orders[0].Transaction.Id);
      Assert.Equal(orderId, Account.ActivePositions[orderId].Order.Transaction.Id);
      Assert.Equal(position, Account.ActivePositions[orderId]);
    }
  }
}
