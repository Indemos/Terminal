using Simulation;
using System;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class IsOrderExecutable : Adapter, IDisposable
  {
    public IsOrderExecutable()
    {
      Account = new Account
      {
        Descriptor = "Demo",
        Balance = 50000
      };
    }

    private void SetStates(params InstrumentModel[] instruments)
    {
      foreach (var o in instruments)
      {
        Account.States.Get(o.Name).Instrument = new InstrumentModel
        {
          Name = o.Name,
          Point = o.Point,
        };
      }
    }

    [Theory]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Stop, 15.0, null, 5.0, true)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Stop, 15.0, null, 15.0, true)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Stop, 15.0, null, 25.0, false)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Stop, 5.0, null, 15.0, true)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Stop, 15.0, null, 15.0, true)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Stop, 25.0, null, 15.0, false)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Limit, 5.0, null, 15.0, true)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Limit, 15.0, null, 15.0, true)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.Limit, 25.0, null, 15.0, false)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Limit, 25.0, null, 15.0, true)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Limit, 15.0, null, 15.0, true)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.Limit, 5.0, null, 15.0, false)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0, false)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.StopLimit, 15.0, 15.0, 5.0, false)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.StopLimit, 15.0, 25.0, 5.0, false)]
    [InlineData(OrderSideEnum.Long, OrderTypeEnum.StopLimit, 15.0, 15.0, 15.0, true)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.StopLimit, 15.0, 5.0, 25.0, false)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.StopLimit, 15.0, 10.0, 25.0, false)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.StopLimit, 15.0, 15.0, 25.0, false)]
    [InlineData(OrderSideEnum.Short, OrderTypeEnum.StopLimit, 15.0, 15.0, 15.0, true)]
    public void CheckPendingOrder(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? price,
      double? activationPrice,
      double? orderPrice,
      bool expectation)
    {
      var point = new PointModel()
      {
        Bid = price,
        Ask = price,
        Last = price,
        Time = DateTime.Now,
      };

      var instrument = new InstrumentModel
      {
        Name = "X",
        Point = point
      };

      SetStates(instrument);

      var order = new OrderModel
      {
        Amount = 1,
        Side = orderSide,
        Type = orderType,
        Price = orderPrice,
        ActivationPrice = activationPrice,
        Transaction = new TransactionModel
        {
          Instrument = instrument
        }
      };

      var response = base.IsOrderExecutable(order);

      Assert.Equal(expectation, response);
    }
  }
}
