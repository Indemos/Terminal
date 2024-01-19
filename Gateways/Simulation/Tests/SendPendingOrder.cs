using System;
using Terminal.Connector.Simulation;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class SendPendingOrder : Adapter, IDisposable
  {
    public SendPendingOrder()
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
  }
}
