using System;
using System.Linq;
using Terminal.Connector.Simulation;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class UpdateOrders : Adapter, IDisposable
  {
    public UpdateOrders()
    {
      Account = new Account
      {
        Name = "Demo",
        Balance = 50000
      };
    }

    [Fact]
    public void UpdateOrderProps()
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

      var orderX = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Stop,
        Transaction = new() { Volume = 1, Price = price + 5, Instrument = instrument }
      };

      var orderY = new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Stop,
        Transaction = new() { Volume = 1, Price = price - 5, Instrument = instrument }
      };

      var SL = CorrectOrders(new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Limit,
        Transaction = new() { Volume = 1, Price = price + 5, Instrument = instrument }

      }).First();

      var TP = CorrectOrders(new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Limit,
        Transaction = new() { Volume = 1, Price = price + 15, Instrument = instrument }

      }).First();

      var order = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Stop,
        Orders = new[] { SL, TP },
        Transaction = new() { Volume = 1, Price = price + 10, Instrument = instrument, Id = orderY.Transaction.Id }
      };

      var xOrderId = orderX.Transaction.Id;
      var yOrderId = orderY.Transaction.Id;
      var orderId = order.Transaction.Id;

      base.CreateOrders(orderX);
      base.CreateOrders(orderY);
      base.UpdateOrders(order);

      var update = Account.ActiveOrders[orderId];

      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActivePositions);
      Assert.Equal(2, Account.Orders.Count);
      Assert.Equal(2, Account.ActiveOrders.Count);
      Assert.Equal(yOrderId, Account.ActiveOrders[yOrderId].Transaction.Id);
      Assert.Equal(xOrderId, Account.ActiveOrders[xOrderId].Transaction.Id);
      Assert.Equal(order.Transaction.Instrument.Name, update.Transaction.Instrument.Name);
      Assert.Equal(order.Type, update.Type);
      Assert.Equal(order.Side, update.Side);
      Assert.Equal(order.Transaction.Volume, update.Transaction.Volume);
      Assert.Equal(order.Transaction.Price, update.Transaction.Price);
      Assert.Equal(order.Transaction.Instrument, update.Transaction.Instrument);
      Assert.Equal(order.ActivationPrice, update.ActivationPrice);
      Assert.Equal(order.Orders, update.Orders);
    }
  }
}
