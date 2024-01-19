using System;
using System.Linq;
using Terminal.Connector.Simulation;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class DecreasePosition : Adapter, IDisposable
  {
    public DecreasePosition()
    {
      Account = new Account
      {
        Name = "Demo",
        Balance = 50000
      };
    }

    [Fact]
    public void Decrease()
    {
      var (orderY, instrumentY) = CreatePositions();
      var order = CorrectOrders(new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 1, Instrument = instrumentY }

      }).First();

      var previousPosition = Account.ActivePositions[orderY.Transaction.Id];
      var nextPosition = base.DecreasePosition(order, previousPosition);

      Assert.Single(Account.Positions);
      Assert.Equal(3, Account.Orders.Count);
      Assert.Equal(0, Account.ActiveOrders.Count);
      Assert.Equal(2, Account.ActivePositions.Count);
      Assert.Equal(2, nextPosition.Orders.Count);

      // Order #1

      var openA = nextPosition.Orders[0];

      Assert.Equal(openA.Transaction.Id, orderY.Transaction.Id);
      Assert.Equal(openA.Transaction.Time, orderY.Transaction.Time);
      Assert.Equal(openA.Transaction.Volume, orderY.Transaction.Volume);
      Assert.Equal(openA.Transaction.Price, instrumentY.Points[0].Ask);

      // Order #2

      var openB = nextPosition.Orders[1];

      Assert.Equal(openB.Transaction.Id, order.Transaction.Id);
      Assert.Equal(openB.Transaction.Time, order.Transaction.Time);
      Assert.Equal(openB.Transaction.Volume, order.Transaction.Volume);
      Assert.Equal(openB.Transaction.Price, instrumentY.Points[1].Ask);

      // Position

      Assert.Equal(nextPosition.Order.Transaction.Id, order.Transaction.Id);
      Assert.Equal(nextPosition.Order.Transaction.Time, order.Transaction.Time);
      Assert.Equal(nextPosition.Order.Transaction.Price, order.Transaction.Price);
      Assert.Equal(nextPosition.Order.Transaction.Volume, Math.Abs(orderY.Transaction.Volume.Value - order.Transaction.Volume.Value));

      // Gain

      Assert.Equal(previousPosition.GainLossEstimate, previousPosition.GainLoss);
      Assert.Equal(previousPosition.GainLossPointsEstimate, previousPosition.GainLossPoints);
    }

    [Fact]
    public void Close()
    {
      var (orderY, instrumentY) = CreatePositions();
      var order = CorrectOrders(new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 2, Instrument = instrumentY }

      }).First();

      var previousPosition = Account.ActivePositions[orderY.Transaction.Id];
      var nextPosition = base.DecreasePosition(order, previousPosition);

      Assert.Single(Account.Positions);
      Assert.Equal(3, Account.Orders.Count);
      Assert.Equal(0, Account.ActiveOrders.Count);
      Assert.Equal(1, Account.ActivePositions.Count);
      Assert.Equal(2, nextPosition.Orders.Count);

      // Order #1

      var openA = nextPosition.Orders[0];

      Assert.Equal(openA.Transaction.Id, orderY.Transaction.Id);
      Assert.Equal(openA.Transaction.Time, orderY.Transaction.Time);
      Assert.Equal(openA.Transaction.Volume, orderY.Transaction.Volume);
      Assert.Equal(openA.Transaction.Price, instrumentY.Points[0].Ask);

      // Order #2

      var openB = nextPosition.Orders[1];

      Assert.Equal(openB.Transaction.Id, order.Transaction.Id);
      Assert.Equal(openB.Transaction.Time, order.Transaction.Time);
      Assert.Equal(openB.Transaction.Volume, order.Transaction.Volume);
      Assert.Equal(openB.Transaction.Price, instrumentY.Points[1].Ask);

      // Position

      Assert.Equal(nextPosition.Order.Transaction.Id, order.Transaction.Id);
      Assert.Equal(nextPosition.Order.Transaction.Time, order.Transaction.Time);
      Assert.Equal(nextPosition.Order.Transaction.Price, order.Transaction.Price);
      Assert.Equal(nextPosition.Order.Transaction.Volume, 0);

      // Gain

      Assert.Equal(previousPosition.GainLossEstimate, previousPosition.GainLoss);
      Assert.Equal(previousPosition.GainLossPointsEstimate, previousPosition.GainLossPoints);
    }

    [Fact]
    public void Inverse()
    {
      var (orderY, instrumentY) = CreatePositions();
      var order = CorrectOrders(new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 3, Instrument = instrumentY }

      }).First();

      var previousPosition = Account.ActivePositions[orderY.Transaction.Id];
      var nextPosition = base.DecreasePosition(order, previousPosition);

      Assert.Single(Account.Positions);
      Assert.Equal(3, Account.Orders.Count);
      Assert.Equal(0, Account.ActiveOrders.Count);
      Assert.Equal(2, Account.ActivePositions.Count);
      Assert.Equal(2, nextPosition.Orders.Count);

      // Order #1

      var openA = nextPosition.Orders[0];

      Assert.Equal(openA.Transaction.Id, orderY.Transaction.Id);
      Assert.Equal(openA.Transaction.Time, orderY.Transaction.Time);
      Assert.Equal(openA.Transaction.Volume, orderY.Transaction.Volume);
      Assert.Equal(openA.Transaction.Price, instrumentY.Points[0].Ask);

      // Order #2

      var openB = nextPosition.Orders[1];

      Assert.Equal(openB.Transaction.Id, order.Transaction.Id);
      Assert.Equal(openB.Transaction.Time, order.Transaction.Time);
      Assert.Equal(openB.Transaction.Volume, order.Transaction.Volume);
      Assert.Equal(openB.Transaction.Price, instrumentY.Points[1].Ask);

      // Position

      Assert.Equal(nextPosition.Order.Transaction.Id, order.Transaction.Id);
      Assert.Equal(nextPosition.Order.Transaction.Time, order.Transaction.Time);
      Assert.Equal(nextPosition.Order.Transaction.Price, order.Transaction.Price);
      Assert.Equal(nextPosition.Order.Transaction.Volume, Math.Abs(orderY.Transaction.Volume.Value - order.Transaction.Volume.Value));

      // Gain

      Assert.Equal(previousPosition.GainLossEstimate, previousPosition.GainLoss);
      Assert.Equal(previousPosition.GainLossPointsEstimate, previousPosition.GainLossPoints);
    }

    private (OrderModel, IInstrument) CreatePositions()
    {
      var price = 15;
      var pointX = new PointModel { Bid = price, Ask = price };
      var pointY = new PointModel { Bid = price, Ask = price };
      var instrumentX = new Instrument()
      {
        Name = "X",
        Points = new ObservableTimeCollection<PointModel> { pointX }
      };

      var instrumentY = new Instrument()
      {
        Name = "Y",
        Points = new ObservableTimeCollection<PointModel> { pointY }
      };

      var orderX = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 2, Instrument = instrumentX }
      };

      var orderY = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 2, Instrument = instrumentY }
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
