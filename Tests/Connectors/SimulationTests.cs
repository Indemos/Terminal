using System;
using System.Diagnostics.Metrics;
using System.Linq;
using Terminal.Connector.Simulation;
using Terminal.Core.CollectionSpace;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Tests.Connectors
{
  public class SimulationTests : Adapter, IDisposable
  {
    const string AssetX = "X";
    const string AssetY = "Y";

    public SimulationTests()
    {
      var askX = 20;
      var bidX = 10;
      var askY = 100;
      var bidY = 50;
      var span = TimeSpan.FromSeconds(1);
      var instrumentX = new InstrumentModel { Name = AssetX, TimeFrame = span, Ask = askX, Bid = bidX };
      var instrumentY = new InstrumentModel { Name = AssetY, TimeFrame = span, Ask = askY, Bid = bidY };
      var pointX = new PointModel { Ask = askX, Bid = bidX, Last = bidX, Instrument = instrumentX };
      var pointY = new PointModel { Ask = askY, Bid = bidY, Last = askY, Instrument = instrumentX };

      instrumentX.Points.Add(pointX);
      instrumentY.Points.Add(pointY);
      instrumentX.PointGroups.Add(pointX);
      instrumentY.PointGroups.Add(pointY);

      Account = new AccountModel
      {
        Name = "Demo",
        Balance = 50000,
        Instruments = new NameCollection<string, IInstrumentModel>
        {
          [AssetX] = instrumentX,
          [AssetY] = instrumentY
        }
      };
    }

    public static ITransactionOrderModel GenerateOrder(
      string asset,
      OrderSideEnum? orderSide,
      OrderTypeEnum? orderType,
      double? volume,
      double? bid,
      double? ask,
      double? activationPrice,
      double? orderPrice)
    {
      var instrument = new InstrumentModel
      {
        Name = asset,
        TimeFrame = TimeSpan.FromSeconds(1)
      };

      var point = new PointModel
      {
        Ask = ask,
        Bid = bid,
        Last = bid,
        Instrument = instrument
      };

      var order = new TransactionOrderModel
      {
        Volume = volume,
        Side = orderSide,
        Type = orderType,
        Instrument = instrument,
        Price = orderPrice,
        ActivationPrice = activationPrice
      };

      instrument.Points.Add(point);
      instrument.PointGroups.Add(point);

      return order;
    }

    [Fact]
    public void ValidateOrdersWithoutProps()
    {
      var instrument = new InstrumentModel();
      var order = new TransactionOrderModel();
      var error = "NotEmptyValidator";
      var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

      Assert.Contains($"{nameof(order.Side)} {error}", errors);
      Assert.Contains($"{nameof(order.Volume)} {error}", errors);
      Assert.Contains($"{nameof(order.Type)} {error}", errors);
      Assert.Contains($"{nameof(instrument.Name)} {error}", errors);
      Assert.Contains($"{nameof(instrument.Points)} {error}", errors);
    }

    [Fact]
    public void ValidateOrdersWithoutQuotes()
    {
      var point = new PointModel();
      var instrument = new InstrumentModel();
      var order = new TransactionOrderModel
      {
        Instrument = instrument
      };

      instrument.Points.Add(point);
      instrument.PointGroups.Add(point);

      var error = "NotEmptyValidator";
      var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

      Assert.Contains($"{nameof(order.Side)} {error}", errors);
      Assert.Contains($"{nameof(order.Volume)} {error}", errors);
      Assert.Contains($"{nameof(order.Type)} {error}", errors);
      Assert.Contains($"{nameof(instrument.Name)} {error}", errors);
      Assert.Contains($"{nameof(point.Bid)} {error}", errors);
      Assert.Contains($"{nameof(point.Ask)} {error}", errors);
      Assert.Contains($"{nameof(point.Last)} {error}", errors);
    }

    [Theory]
    [InlineData(OrderTypeEnum.Stop)]
    [InlineData(OrderTypeEnum.Limit)]
    [InlineData(OrderTypeEnum.StopLimit)]
    public void ValidateOrdersWithoutOpenPrice(OrderTypeEnum orderType)
    {
      var order = new TransactionOrderModel
      {
        Type = orderType
      };

      var error = "NotEmptyValidator";
      var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

      Assert.Contains($"{nameof(order.Price)} {error}", errors);
    }

    [Theory]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Stop, 5.0, 10.0, "GreaterThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Stop, 10.0, 5.0, "LessThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Limit, 10.0, 5.0, "LessThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Limit, 5.0, 10.0, "GreaterThanOrEqualValidator")]
    public void ValidateOrdersWithIncorrectPrice(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double orderPrice,
      double price,
      string error)
    {
      var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, price, price, null, orderPrice);
      var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

      Assert.Contains($"{nameof(order.Price)} {error}", errors);
    }

    [Theory]
    [InlineData(OrderSideEnum.Buy, 15.0, null, 10.0, "NotEmptyValidator", "GreaterThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, 15.0, null, 5.0, "NotEmptyValidator", "LessThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Buy, 5.0, 10.0, 15.0, "GreaterThanOrEqualValidator", "GreaterThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, 15.0, 10.0, 5.0, "LessThanOrEqualValidator", "LessThanOrEqualValidator")]
    public void ValidateOrdersWithIncorrectStopLimitPrice(
      OrderSideEnum orderSide,
      double? orderPrice,
      double? activationPrice,
      double? price,
      string activationError,
      string orderError)
    {
      var order = GenerateOrder(AssetX, orderSide, OrderTypeEnum.StopLimit, 1.0, price, price, activationPrice, orderPrice);
      var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

      Assert.Contains($"{nameof(order.ActivationPrice)} {activationError}", errors);
      Assert.Contains($"{nameof(order.Price)} {orderError}", errors);
    }

    [Fact]
    public void CreateOrdersWithEmptyOrder()
    {
      var order = new TransactionOrderModel();

      base.CreateOrders(order);

      Assert.Empty(Account.Orders);
      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Empty(Account.ActivePositions);
      Assert.Null(order.Status);
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
    public void CreateOrdersWithoutMatching(
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
      var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, price, price, activationPrice, orderPrice);

      base.CreateOrders(order);

      Assert.Equal(orders, Account.Orders.Count);
      Assert.Equal(activeOrders, Account.ActiveOrders.Count);
      Assert.Equal(positions, Account.Positions.Count);
      Assert.Equal(activePositions, Account.ActivePositions.Count);
    }

    [Theory]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Stop, 15.0, null, 25.0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 5.0)]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 5.0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Limit, 15.0, null, 25.0)]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0)]
    public void SendPendingOrderUpdatingStatements(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? price,
      double? activationPrice,
      double? orderPrice)
    {
      var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, price, price, activationPrice, orderPrice);
      var orderId = order.Id;

      base.SendPendingOrder(order);

      Assert.Equal(order.Status, OrderStatusEnum.Placed);
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
    public void CreatePositionWithoutMatching(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? bid,
      double? ask,
      double? price)
    {
      var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, bid, ask, null, null);
      var point = order.Instrument.Points.First();
      var orderId = order.Id;

      base.CreatePosition(order);

      var position = Account.ActivePositions[orderId];

      Assert.Equal(order.Price, price);
      Assert.Equal(order.Status, OrderStatusEnum.Filled);

      Assert.Equal(position.Time, order.Time);
      Assert.Equal(position.Price, price);
      Assert.Equal(position.OpenPrice, price);
      Assert.Equal(position.OpenPrices.Last().Price, price);
      Assert.Single(position.OpenPrices);

      Assert.Equal(order, Account.Orders[0]);
      Assert.Equal(orderId, Account.ActivePositions[orderId].Id);
      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Single(Account.Orders);
      Assert.Single(Account.ActivePositions);
    }

    [Fact]
    public void CreatePositionWithPendingOrders()
    {
      var price = 15.0;
      var order = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 1.0, price, price, null, null);
      var SL = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 1.0, price, price, null, 5.0);
      var TP = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Limit, 1.0, price, price, null, 25.0);
      var orderId = order.Id;

      order.Orders.Add(SL);
      order.Orders.Add(TP);

      var position = base.CreatePosition(order);

      Assert.Empty(Account.Positions);
      Assert.Empty(Account.ActiveOrders);
      Assert.Single(Account.Orders);
      Assert.Single(Account.ActivePositions);
      Assert.Equal(order, Account.Orders[0]);
      Assert.Equal(orderId, Account.ActivePositions[orderId].Id);
      Assert.Equal(position, Account.ActivePositions[orderId]);
    }

    [Fact]
    public void GetPositionFromOrder()
    {
      var SL = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 2.0, 15.0, 15.0, 5.0, null);
      var order = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 1.0, 15.0, 15.0, null, 25.0);
      var orderId = order.Id;

      order.Orders.Add(SL);

      var response = base.GetPosition(order);

      Assert.Equal(orderId, response.Id);
      Assert.Equal(order.Name, response.Name);
      Assert.Equal(order.Description, response.Description);
      Assert.Equal(order.Group, response.Group);
      Assert.Equal(order.Type, response.Type);
      Assert.Equal(order.Side, response.Side);
      Assert.Equal(order.Volume, response.Volume);
      Assert.Equal(order.Price, response.Price);
      Assert.Equal(order.Instrument, response.Instrument);
      Assert.Equal(order.ActivationPrice, response.ActivationPrice);
      Assert.Equal(order.Orders, response.Orders);
    }

    [Fact]
    public void UpdateOrdersWithMatches()
    {
      var SL = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 2.0, 15.0, 15.0, 5.0, null);
      var order = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 2.0, 5.0, 5.0, 10.0, 15.0);
      var orderX = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 1.0, 15.0, 15.0, null, 25.0);
      var orderY = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 1.0, 15.0, 15.0, null, 5.0);

      order.Id = orderY.Id;
      order.Orders.Add(SL);

      var orderId = order.Id;
      var orderIdX = orderX.Id;
      var orderIdY = orderY.Id;

      Account.ActiveOrders.Add(orderIdX, orderX);
      Account.ActiveOrders.Add(orderIdY, orderY);

      base.UpdateOrders(order);

      var update = Account.ActiveOrders[orderIdY];

      Assert.Equal(2, Account.ActiveOrders.Count);
      Assert.Equal(orderIdY, Account.ActiveOrders[orderIdY].Id);
      Assert.Equal(orderIdX, Account.ActiveOrders[orderIdX].Id);
      Assert.Equal(orderId, update.Id);
      Assert.Equal(order.Name, update.Name);
      Assert.Equal(order.Description, update.Description);
      Assert.Equal(order.Group, update.Group);
      Assert.Equal(order.Type, update.Type);
      Assert.Equal(order.Side, update.Side);
      Assert.Equal(order.Volume, update.Volume);
      Assert.Equal(order.Price, update.Price);
      Assert.Equal(order.Instrument, update.Instrument);
      Assert.Equal(order.ActivationPrice, update.ActivationPrice);
      Assert.Equal(order.Orders, update.Orders);
      Assert.Empty(Account.ActivePositions);
      Assert.Empty(Account.Positions);
      Assert.Empty(Account.Orders);
    }

    [Fact]
    public void IncreasePositionWithMatches()
    {
      var instrumentX = Account.Instruments[AssetX];
      var instrumentY = Account.Instruments[AssetY];
      var pointX = instrumentX.Points.Last();
      var pointY = instrumentY.Points.Last();
      var orderX = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 1, pointX.Bid, pointX.Ask, null, null);
      var orderY = GenerateOrder(AssetY, OrderSideEnum.Buy, OrderTypeEnum.Market, 2, pointY.Bid, pointY.Ask, null, null);
      var increase = GenerateOrder(AssetY, OrderSideEnum.Buy, OrderTypeEnum.Market, 3, pointY.Bid + 5, pointY.Ask + 5, null, null);

      // Open

      base.CreateOrders(orderX);
      base.CreateOrders(orderY);

      // Increase

      var previousPosition = Account.ActivePositions[orderY.Id];
      var nextPosition = base.IncreasePosition(increase, previousPosition);
      var averageTradePrice = nextPosition.OpenPrices.Sum(o => o.Volume * o.Price) / nextPosition.OpenPrices.Sum(o => o.Volume);

      Assert.Equal(3, Account.Orders.Count);
      Assert.Equal(0, Account.ActiveOrders.Count);
      Assert.Equal(0, Account.Positions.Count);
      Assert.Equal(2, Account.ActivePositions.Count);

      var openA = nextPosition.OpenPrices[0];
      var openB = nextPosition.OpenPrices[1];

      Assert.Equal(orderY.Volume, openA.Volume);
      Assert.Equal(increase.Volume, openB.Volume);
      Assert.Equal(pointY.Ask, openA.Price);
      Assert.Equal(pointY.Ask + 5, openB.Price);
      Assert.Equal(orderY.Time, openA.Time);
      Assert.Equal(increase.Time, openB.Time);
      Assert.Equal(2, nextPosition.OpenPrices.Count);

      Assert.Equal(increase.Id, nextPosition.Id);
      Assert.Equal(increase.Time, nextPosition.Time);
      Assert.Equal(averageTradePrice, nextPosition.OpenPrice);
      Assert.Equal(increase.Volume + orderY.Volume, nextPosition.Volume);

      Assert.Equal(nextPosition.Time, previousPosition.CloseTime);
      Assert.Equal(openB.Price, previousPosition.ClosePrice);
      Assert.Equal(previousPosition.GainLossEstimate, previousPosition.GainLoss);
      Assert.Equal(previousPosition.GainLossPointsEstimate, previousPosition.GainLossPoints);

      // Estimate

      var step = instrumentY.StepValue / instrumentY.StepSize;
      var priceUpdate = new PointModel { Ask = 50, Bid = 40, Last = 40, Instrument = instrumentY };

      instrumentY.Points.Add(priceUpdate);
      instrumentY.PointGroups.Add(priceUpdate);

      nextPosition.Instrument = instrumentY;

      Assert.Equal(nextPosition.GainLossPointsEstimate * nextPosition.Volume * step - instrumentY.Commission, nextPosition.GainLossEstimate);
      Assert.Equal(priceUpdate.Bid - nextPosition.OpenPrice, nextPosition.GainLossPointsEstimate);
    }

    [Fact]
    public void DecreasePositionWithMatches()
    {
      var instrumentX = Account.Instruments[AssetX];
      var instrumentY = Account.Instruments[AssetY];
      var pointX = instrumentX.Points.Last();
      var pointY = instrumentY.Points.Last();
      var orderX = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 1, pointX.Bid, pointX.Ask, null, null);
      var orderY = GenerateOrder(AssetY, OrderSideEnum.Buy, OrderTypeEnum.Market, 1, pointY.Bid, pointY.Ask, null, null);
      var decreaseA = GenerateOrder(AssetY, OrderSideEnum.Sell, OrderTypeEnum.Market, 2, pointY.Bid + 5, pointY.Ask + 5, null, null);
      var decreaseB = GenerateOrder(AssetY, OrderSideEnum.Buy, OrderTypeEnum.Market, 1, pointY.Bid + 15, pointY.Ask + 15, null, null);

      // Open

      base.CreateOrders(orderX);
      base.CreateOrders(orderY);

      // Inverse

      var previousPosition = Account.ActivePositions[orderY.Id];
      var nextPosition = base.DecreasePosition(decreaseA, previousPosition);

      Assert.Equal(3, Account.Orders.Count);
      Assert.Equal(0, Account.ActiveOrders.Count);
      Assert.Equal(1, Account.Positions.Count);
      Assert.Equal(2, Account.ActivePositions.Count);

      var openA = nextPosition.OpenPrices[0];

      Assert.Equal(decreaseA.Volume, openA.Volume);
      Assert.Equal(pointY.Bid + 5, openA.Price);
      Assert.Equal(decreaseA.Time, openA.Time);
      Assert.Single(nextPosition.OpenPrices);

      Assert.Equal(decreaseA.Id, nextPosition.Id);
      Assert.Equal(decreaseA.Time, nextPosition.Time);
      Assert.Equal(openA.Price, nextPosition.OpenPrice);
      Assert.Equal(Math.Abs(orderY.Volume.Value - decreaseA.Volume.Value), nextPosition.Volume);

      Assert.Equal(nextPosition.Time, previousPosition.CloseTime);
      Assert.Equal(openA.Price, previousPosition.ClosePrice);
      Assert.Equal(previousPosition.GainLossEstimate, previousPosition.GainLoss);
      Assert.Equal(previousPosition.GainLossPointsEstimate, previousPosition.GainLossPoints);

      // Close

      previousPosition = Account.ActivePositions[decreaseA.Id];
      nextPosition = base.DecreasePosition(decreaseB, previousPosition);

      Assert.Equal(4, Account.Orders.Count);
      Assert.Equal(0, Account.ActiveOrders.Count);
      Assert.Equal(2, Account.Positions.Count);
      Assert.Equal(1, Account.ActivePositions.Count);
    }
  }
}
