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
        Name = instrument.Name,
        Side = OrderSideEnum.Long,
        Account = Account
      };

      var separateOrder = new OrderModel
      {
        Id = "X2",
        Amount = 1,
        Name = instrument.Name,
        Side = OrderSideEnum.Short,
        Account = Account
      };

      var noSideOrder = new OrderModel
      {
        Id = "X3",
        Amount = 1,
        Name = instrument.Name,
        Account = Account
      };

      var noAmountOrder = new OrderModel
      {
        Id = "X4",
        Amount = 1,
        Name = instrument.Name,
        Account = Account
      };

      var noInstrumentOrder = new OrderModel
      {
        Id = "X5",
        Amount = 1,
        Account = Account
      };

      var order = new OrderModel
      {
        Id = "X",
        Amount = 1,
        Descriptor = "DemoDescriptor",
        OpenPrice = price,
        Type = OrderTypeEnum.Market,
        TimeSpan = OrderTimeSpanEnum.Fok,
        Instruction = InstructionEnum.Group,
        Time = point.Time,
        Orders = [subOrder, separateOrder, noSideOrder],
        Name = instrument.Name,
        Account = Account
      };

      var res = ComposeOrders(order);
      var comboSub = res.FirstOrDefault(o => o.Id == "X1");
      var comboSeparate = res.FirstOrDefault(o => o.Id == "X2");
      var comboNoSide = res.FirstOrDefault(o => o.Id == "X3");
      var comboNoAmount = res.FirstOrDefault(o => o.Id == "X4");

      Assert.Equal(4, res.Count);
      Assert.Null(comboNoAmount);

      Assert.Equal(comboSub.Descriptor, order.Descriptor);
      Assert.Equal(comboSub.OpenPrice, GetOpenPrice(subOrder.Side, subOrder.Instrument.Point));
      Assert.Equal(comboSub.Type, order.Type);
      Assert.Equal(comboSub.TimeSpan, order.TimeSpan);
      Assert.Equal(comboSub.Instruction, InstructionEnum.Side);
      Assert.Equal(comboSub.Time, order.Time);

      Assert.Equal(comboSeparate.Descriptor, order.Descriptor);
      Assert.Equal(comboSeparate.OpenPrice, GetOpenPrice(separateOrder.Side, subOrder.Instrument.Point));
      Assert.Equal(comboSeparate.Type, order.Type);
      Assert.Equal(comboSeparate.TimeSpan, order.TimeSpan);
      Assert.Equal(comboSeparate.Instruction, InstructionEnum.Side);
      Assert.Equal(comboSeparate.Time, order.Time);

      Assert.Equal(comboNoSide.Descriptor, order.Descriptor);
      Assert.Null(comboNoSide.OpenPrice);
      Assert.Equal(comboNoSide.Type, order.Type);
      Assert.Equal(comboSeparate.TimeSpan, order.TimeSpan);
      Assert.Equal(comboNoSide.Instruction, InstructionEnum.Side);
      Assert.Equal(comboNoSide.Time, order.Time);
    }
  }
}
