using Simulation;
using System;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class ComposeOrders : Adapter, IDisposable
  {
    public ComposeOrders()
    {
      Account = new Account
      {
        Descriptor = "Demo",
        Balance = 50000
      };
    }

    private double? GetOpenPrice(OrderSideEnum? side, PointModel point)
    {
      switch (side)
      {
        case OrderSideEnum.Long: return point.Ask;
        case OrderSideEnum.Short: return point.Bid;
      }

      return null;
    }

    [Fact]
    public void ComposeFromGroup()
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

      Account.States.Get(instrument.Name).Instrument = instrument;

      var subOrder = new OrderModel
      {
        Id = "X1",
        Amount = 1,
        Side = OrderSideEnum.Long,
        Transaction = new TransactionModel
        {
          Instrument = instrument
        }
      };

      var separateOrder = new OrderModel
      {
        Id = "X2",
        Amount = 1,
        Side = OrderSideEnum.Short,
        Transaction = new TransactionModel
        {
          Instrument = instrument
        }
      };

      var noSideOrder = new OrderModel
      {
        Id = "X3",
        Amount = 1,
        Transaction = new TransactionModel
        {
          Instrument = instrument
        }
      };

      var noAmountOrder = new OrderModel
      {
        Id = "X4",
        Amount = 1,
        Transaction = new TransactionModel
        {
          Instrument = instrument
        }
      };

      var noInstrumentOrder = new OrderModel
      {
        Id = "X5",
        Amount = 1,
      };

      var order = new OrderModel
      {
        Id = "X",
        Amount = 1,
        Descriptor = "DemoDescriptor",
        Price = price,
        Type = OrderTypeEnum.Market,
        TimeSpan = OrderTimeSpanEnum.Fok,
        Instruction = InstructionEnum.Group,
        Orders = [subOrder, separateOrder, noSideOrder],
        Transaction = new TransactionModel
        {
          Instrument = instrument
        }
      };

      var res = ComposeOrders(order);
      var comboSub = res.FirstOrDefault(o => o.Id == "X1");
      var comboSeparate = res.FirstOrDefault(o => o.Id == "X2");
      var comboNoSide = res.FirstOrDefault(o => o.Id == "X3");
      var comboNoAmount = res.FirstOrDefault(o => o.Id == "X4");

      Assert.Equal(4, res.Count);
      Assert.Null(comboNoAmount);

      Assert.Equal(comboSub.Descriptor, order.Descriptor);
      Assert.Equal(comboSub.Price, GetOpenPrice(subOrder.Side, subOrder.Transaction.Instrument.Point));
      Assert.Equal(comboSub.Type, order.Type);
      Assert.Equal(comboSub.TimeSpan, order.TimeSpan);
      Assert.Equal(comboSub.Instruction, InstructionEnum.Side);
      Assert.Equal(comboSub.Transaction.Time, order.Transaction.Instrument.Point.Time);

      Assert.Equal(comboSeparate.Descriptor, order.Descriptor);
      Assert.Equal(comboSeparate.Price, GetOpenPrice(separateOrder.Side, subOrder.Transaction.Instrument.Point));
      Assert.Equal(comboSeparate.Type, order.Type);
      Assert.Equal(comboSeparate.TimeSpan, order.TimeSpan);
      Assert.Equal(comboSeparate.Instruction, InstructionEnum.Side);
      Assert.Equal(comboSeparate.Transaction.Time, order.Transaction.Instrument.Point.Time);

      Assert.Equal(comboNoSide.Descriptor, order.Descriptor);
      Assert.Null(comboNoSide.Price);
      Assert.Equal(comboNoSide.Type, order.Type);
      Assert.Equal(comboSeparate.TimeSpan, order.TimeSpan);
      Assert.Equal(comboNoSide.Instruction, InstructionEnum.Side);
      Assert.Equal(comboNoSide.Transaction.Time, order.Transaction.Instrument.Point.Time);
    }
  }
}
