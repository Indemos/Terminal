using System;
using Simulation;
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
        Descriptor = "Demo",
        Balance = 50000
      };
    }

    [Fact]
    public void CreateOrdersWithEmptyOrder()
    {
      var order = new OrderModel();

      base.SendOrders(order);

      Assert.Empty(Account.Orders);
      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Empty(Account.ActivePositions);
    }

    [Theory]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Market, 5, null, null, 1, 0, 0, 1)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Market, 5, null, null, 1, 0, 0, 1)]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Stop, 5, null, 15, 1, 1, 0, 0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Stop, 15, null, 5, 1, 1, 0, 0)]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Limit, 15, null, 5, 1, 1, 0, 0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Limit, 5, null, 15, 1, 1, 0, 0)]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 5, 10, 15, 1, 1, 0, 0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15, 10, 5, 1, 1, 0, 0)]
    public void CreateBasicOrders(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      decimal? price,
      decimal? activationPrice,
      decimal? orderPrice,
      int orders,
      int activeOrders,
      int positions,
      int activePositions)
    {
      var order = new OrderModel
      {
        Side = orderSide,
        Type = orderType,
        Price = orderPrice,
        ActivationPrice = activationPrice,
        Transaction = new()
        {
          Volume = 1,
          Instrument = new Instrument()
          {
            Name = "X",
            Points =
            [
              new() { Bid = price, Ask = price }
            ]
          }
        }
      };

      base.SendOrders(order);

      Assert.Equal(orders, Account.Orders.Count);
      Assert.Equal(activeOrders, Account.ActiveOrders.Count);
      Assert.Equal(positions, Account.Positions.Count);
      Assert.Equal(activePositions, Account.ActivePositions.Count);
    }
  }
}
