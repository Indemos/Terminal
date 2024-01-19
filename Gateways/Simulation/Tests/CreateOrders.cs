using System;
using Terminal.Connector.Simulation;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class CreateOrders : Adapter, IDisposable
  {
    public CreateOrders()
    {
      Account = new Account
      {
        Name = "Demo",
        Balance = 50000
      };
    }

    [Fact]
    public void CreateOrdersWithEmptyOrder()
    {
      var order = new OrderModel();

      base.CreateOrders(order);

      Assert.Empty(Account.Orders);
      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Empty(Account.ActivePositions);
    }

    [Theory]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Market, 5.0, null, null, 1, 0, 0, 1)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Market, 5.0, null, null, 1, 0, 0, 1)]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Stop, 5.0, null, 15.0, 1, 1, 0, 0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 5.0, 1, 1, 0, 0)]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 5.0, 1, 1, 0, 0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Limit, 5.0, null, 15.0, 1, 1, 0, 0)]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 5.0, 10.0, 15.0, 1, 1, 0, 0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0, 1, 1, 0, 0)]
    public void CreateBasicOrders(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? price,
      double? activationPrice,
      double? orderPrice,
      int orders,
      int activeOrders,
      int positions,
      int activePositions)
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

      base.CreateOrders(order);

      Assert.Equal(orders, Account.Orders.Count);
      Assert.Equal(activeOrders, Account.ActiveOrders.Count);
      Assert.Equal(positions, Account.Positions.Count);
      Assert.Equal(activePositions, Account.ActivePositions.Count);
    }
  }
}
