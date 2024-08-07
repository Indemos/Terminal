using System;
using System.Linq;
using Simulation;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class IncreasePosition : Adapter, IDisposable
  {
    public IncreasePosition()
    {
      Account = new Account
      {
        Descriptor = "Demo",
        Balance = 50000
      };
    }

    [Fact]
    public void Increase()
    {
      var (orderY, instrumentY) = CreatePositions();
      var order = CorrectOrders(new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 2, Instrument = instrumentY }

      }).First();

      // Increase

      var nextPosition = base.IncreasePosition(order, Account.ActivePositions[orderY.Transaction.Id]);
      var averageTradePrice =
        nextPosition.Orders.Sum(o => o.Transaction.Volume * o.Transaction.Price) /
        nextPosition.Orders.Sum(o => o.Transaction.Volume);

      // State

      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Equal(3, Account.Orders.Count);
      Assert.Equal(2, Account.ActivePositions.Count);
      Assert.Equal(2, nextPosition.Orders.Count);

      // Order #1

      var openA = nextPosition.Orders[0];

      Assert.Equal(openA.Transaction.Descriptor, orderY.Transaction.Descriptor);
      Assert.Equal(openA.Transaction.Time, orderY.Transaction.Time);
      Assert.Equal(openA.Transaction.Volume, orderY.Transaction.Volume);
      Assert.Equal(openA.Transaction.Price, instrumentY.Points[0].Ask);

      // Order #2

      var openB = nextPosition.Orders[1];

      Assert.Equal(openB.Transaction.Descriptor, order.Transaction.Descriptor);
      Assert.Equal(openB.Transaction.Time, order.Transaction.Time);
      Assert.Equal(openB.Transaction.Volume, order.Transaction.Volume);
      Assert.Equal(openB.Transaction.Price, instrumentY.Points[1].Ask);

      // Position

      Assert.Equal(nextPosition.Order.Transaction.Descriptor, order.Transaction.Descriptor);
      Assert.Equal(nextPosition.Order.Transaction.Time, order.Transaction.Time);
      Assert.Equal(nextPosition.Order.Transaction.Price, averageTradePrice);
      Assert.Equal(nextPosition.Order.Transaction.Volume, order.Transaction.Volume + orderY.Transaction.Volume);

      // Estimate

      var step = instrumentY.StepValue / instrumentY.StepSize;
      var priceUpdate = new PointModel { Ask = 50, Bid = 40, Last = 40, Instrument = instrumentY };

      instrumentY.Points.Add(priceUpdate);
      instrumentY.PointGroups.Add(priceUpdate);

      nextPosition.Order.Transaction.Instrument = instrumentY;

      Assert.Equal(nextPosition.GainLossEstimate, nextPosition.GainLossPointsEstimate * nextPosition.Order.Transaction.Volume * step - instrumentY.Commission);
      Assert.Equal(nextPosition.GainLossPointsEstimate, priceUpdate.Bid - nextPosition.Order.Transaction.Price);
    }

    private (OrderModel, InstrumentModel) CreatePositions()
    {
      var price = 15;
      var pointX = new PointModel { Bid = price, Ask = price };
      var pointY = new PointModel { Bid = price, Ask = price };
      var instrumentX = new InstrumentModel()
      {
        Name = "X",
        Points = [pointX]
      };

      var instrumentY = new InstrumentModel()
      {
        Name = "Y",
        Points = [pointY]
      };

      var orderX = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 1, Instrument = instrumentX }
      };

      var orderY = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 1, Instrument = instrumentY }
      };

      base.CreateOrders(orderX);
      base.CreateOrders(orderY);

      // Price

      var newPrice = new PointModel
      {
        Bid = price + 10,
        Ask = price + 10
      };

      instrumentY.Points.Add(newPrice);

      return (orderY.Clone() as OrderModel, instrumentY);
    }
  }
}
