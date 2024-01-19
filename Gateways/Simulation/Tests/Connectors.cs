using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Connector.Simulation;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class Connectors : Adapter, IDisposable
  {
    const string AssetX = "X";
    const string AssetY = "Y";

    public Connectors()
    {
      Account = new Account
      {
        Name = "Demo",
        Balance = 50000
      };
    }

    //[Theory]
    //[InlineData(OrderTypeEnum.Stop)]
    //[InlineData(OrderTypeEnum.Limit)]
    //[InlineData(OrderTypeEnum.StopLimit)]
    //public void ValidateOrdersWithoutOpenPrice(OrderTypeEnum orderType)
    //{
    //  var order = new OrderModel
    //  {
    //    Type = orderType
    //  };

    //  var error = "NotEmptyValidator";
    //  var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

    //  Assert.Contains($"{nameof(order.Transaction.Price)} {error}", errors);
    //}

    //[Theory]
    //[InlineData(OrderSideEnum.Buy, OrderTypeEnum.Stop, 5.0, 10.0, "GreaterThanOrEqualValidator")]
    //[InlineData(OrderSideEnum.Sell, OrderTypeEnum.Stop, 10.0, 5.0, "LessThanOrEqualValidator")]
    //[InlineData(OrderSideEnum.Buy, OrderTypeEnum.Limit, 10.0, 5.0, "LessThanOrEqualValidator")]
    //[InlineData(OrderSideEnum.Sell, OrderTypeEnum.Limit, 5.0, 10.0, "GreaterThanOrEqualValidator")]
    //public void ValidateOrdersWithIncorrectPrice(
    //  OrderSideEnum orderSide,
    //  OrderTypeEnum orderType,
    //  double orderPrice,
    //  double price,
    //  string error)
    //{
    //  var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, price, price, null, orderPrice);
    //  var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

    //  Assert.Contains($"{nameof(order.Transaction.Price)} {error}", errors);
    //}

    //[Theory]
    //[InlineData(OrderSideEnum.Buy, 15.0, null, 10.0, "NotEmptyValidator", "GreaterThanOrEqualValidator")]
    //[InlineData(OrderSideEnum.Sell, 15.0, null, 5.0, "NotEmptyValidator", "LessThanOrEqualValidator")]
    //[InlineData(OrderSideEnum.Buy, 5.0, 10.0, 15.0, "GreaterThanOrEqualValidator", "GreaterThanOrEqualValidator")]
    //[InlineData(OrderSideEnum.Sell, 15.0, 10.0, 5.0, "LessThanOrEqualValidator", "LessThanOrEqualValidator")]
    //public void ValidateOrdersWithIncorrectStopLimitPrice(
    //  OrderSideEnum orderSide,
    //  double? orderPrice,
    //  double? activationPrice,
    //  double? price,
    //  string activationError,
    //  string orderError)
    //{
    //  var order = GenerateOrder(AssetX, orderSide, OrderTypeEnum.StopLimit, 1.0, price, price, activationPrice, orderPrice);
    //  var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

    //  Assert.Contains($"{nameof(order.ActivationPrice)} {activationError}", errors);
    //  Assert.Contains($"{nameof(order.Transaction.Price)} {orderError}", errors);
    //}

    //[Fact]
    //public void CreateOrdersWithEmptyOrder()
    //{
    //  var order = new OrderModel();

    //  base.CreateOrders(order);

    //  Assert.Empty(Account.Orders);
    //  Assert.Empty(Account.Positions);
    //  Assert.Empty(Account.ActiveOrders);
    //  Assert.Empty(Account.ActivePositions);
    //  Assert.Null(order.Transaction.Status);
    //}

    //[Theory]
    //[InlineData(OrderSideEnum.Buy, OrderTypeEnum.Market, 5.0, null, null, 1, 0, 0, 1)]
    //[InlineData(OrderSideEnum.Sell, OrderTypeEnum.Market, 5.0, null, null, 1, 0, 0, 1)]
    //[InlineData(OrderSideEnum.Buy, OrderTypeEnum.Stop, 5.0, null, 15.0, 1, 1, 0, 0)]
    //[InlineData(OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 5.0, 1, 1, 0, 0)]
    //[InlineData(OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 5.0, 1, 1, 0, 0)]
    //[InlineData(OrderSideEnum.Sell, OrderTypeEnum.Limit, 5.0, null, 15.0, 1, 1, 0, 0)]
    //[InlineData(OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 5.0, 10.0, 15.0, 1, 1, 0, 0)]
    //[InlineData(OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0, 1, 1, 0, 0)]
    //public void CreateOrdersWithoutMatching(
    //  OrderSideEnum orderSide,
    //  OrderTypeEnum orderType,
    //  double? price,
    //  double? activationPrice,
    //  double? orderPrice,
    //  int orders,
    //  int activeOrders,
    //  int positions,
    //  int activePositions)
    //{
    //  var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, price, price, activationPrice, orderPrice);

    //  base.CreateOrders(order);

    //  Assert.Equal(orders, Account.Orders.Count);
    //  Assert.Equal(activeOrders, Account.ActiveOrders.Count);
    //  Assert.Equal(positions, Account.Positions.Count);
    //  Assert.Equal(activePositions, Account.ActivePositions.Count);
    //}

    //[Theory]
    //[InlineData(OrderSideEnum.Buy, OrderTypeEnum.Stop, 15.0, null, 25.0)]
    //[InlineData(OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 5.0)]
    //[InlineData(OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 5.0)]
    //[InlineData(OrderSideEnum.Sell, OrderTypeEnum.Limit, 15.0, null, 25.0)]
    //[InlineData(OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0)]
    //[InlineData(OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0)]
    //public void SendPendingOrderUpdatingStatements(
    //  OrderSideEnum orderSide,
    //  OrderTypeEnum orderType,
    //  double? price,
    //  double? activationPrice,
    //  double? orderPrice)
    //{
    //  var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, price, price, activationPrice, orderPrice);
    //  var orderId = order.Transaction.Descriptor.Id;

    //  base.SendPendingOrder(order);

    //  Assert.Equal(order.Transaction.Status, OrderStatusEnum.Placed);
    //  Assert.Equal(order, Account.Orders[0]);
    //  Assert.Equal(order, Account.ActiveOrders[orderId]);
    //  Assert.Single(Account.Orders);
    //  Assert.Single(Account.ActiveOrders);
    //  Assert.Empty(Account.Positions);
    //  Assert.Empty(Account.ActivePositions);
    //}

    //[Theory]
    //[InlineData(OrderSideEnum.Buy, OrderTypeEnum.Market, 10.0, 15.0, 15.0)]
    //[InlineData(OrderSideEnum.Sell, OrderTypeEnum.Market, 10.0, 15.0, 10.0)]
    //public void CreatePositionWithoutMatching(
    //  OrderSideEnum orderSide,
    //  OrderTypeEnum orderType,
    //  double? bid,
    //  double? ask,
    //  double? price)
    //{
    //  var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, bid, ask, null, null);
    //  var point = order.Transaction.Instrument.Points.First();
    //  var orderId = order.Transaction.Descriptor.Id;

    //  base.CreatePosition(order);

    //  var position = Account.ActivePositions[orderId];
    //  var openPrice = position.Orders.First();
    //  var closePrice = position.Orders.Last();

    //  Assert.Equal(order.Transaction.Price, price);
    //  Assert.Equal(order.Transaction.Status, OrderStatusEnum.Filled);

    //  Assert.Equal(position.Order.Transaction.Time, order.Transaction.Time);
    //  Assert.Equal(position.Order.Transaction.Price, price);
    //  Assert.Equal(openPrice.Transaction.Price, price);
    //  Assert.Equal(closePrice.Transaction.Price, price);
    //  Assert.Single(position.Orders);

    //  Assert.Equal(order, Account.Orders[0]);
    //  Assert.Equal(orderId, Account.ActivePositions[orderId].Order.Transaction.Descriptor.Id);
    //  Assert.Empty(Account.Positions);
    //  Assert.Empty(Account.ActiveOrders);
    //  Assert.Single(Account.Orders);
    //  Assert.Single(Account.ActivePositions);
    //}

    //[Fact]
    //public void CreatePositionWithPendingOrders()
    //{
    //  var price = 15.0;
    //  var order = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 1.0, price, price, null, null);
    //  var SL = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 1.0, price, price, null, 5.0);
    //  var TP = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Limit, 1.0, price, price, null, 25.0);
    //  var orderId = order.Transaction.Descriptor.Id;

    //  order.Orders.Add(SL);
    //  order.Orders.Add(TP);

    //  var position = base.CreatePosition(order);

    //  Assert.Empty(Account.Positions);
    //  Assert.Empty(Account.ActiveOrders);
    //  Assert.Single(Account.Orders);
    //  Assert.Single(Account.ActivePositions);
    //  Assert.Equal(order, Account.Orders[0]);
    //  Assert.Equal(orderId, Account.ActivePositions[orderId].Order.Transaction.Descriptor.Id);
    //  Assert.Equal(position, Account.ActivePositions[orderId]);
    //}

    //[Fact]
    //public void GetPositionFromOrder()
    //{
    //  var SL = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 2.0, 15.0, 15.0, 5.0, null);
    //  var order = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 1.0, 15.0, 15.0, null, 25.0);
    //  var orderId = order.Transaction.Descriptor.Id;

    //  order.Orders.Add(SL);

    //  var response = base.GetPosition(order);

    //  Assert.Equal(orderId, response.Order.Transaction.Descriptor.Id);
    //  Assert.Equal(order.Transaction.Descriptor.Name, response.Order.Transaction.Descriptor.Name);
    //  Assert.Equal(order.Transaction.Descriptor.Description, response.Order.Transaction.Descriptor.Description);
    //  Assert.Equal(order.Transaction.Descriptor.Group, response.Order.Transaction.Descriptor.Group);
    //  Assert.Equal(order.Type, response.Order.Type);
    //  Assert.Equal(order.Side, response.Order.Side);
    //  Assert.Equal(order.Transaction.Volume, response.Order.Transaction.Volume);
    //  Assert.Equal(order.Transaction.Price, response.Order.Transaction.Price);
    //  Assert.Equal(order.Transaction.Instrument, response.Order.Transaction.Instrument);
    //  Assert.Equal(order.ActivationPrice, response.Order.ActivationPrice);
    //  Assert.Equal(order.Orders, response.Orders);
    //}

    //[Fact]
    //public void UpdateOrdersWithMatches()
    //{
    //  var SL = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 2.0, 15.0, 15.0, 5.0, null);
    //  var order = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 2.0, 5.0, 5.0, 10.0, 15.0);
    //  var orderX = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 1.0, 15.0, 15.0, null, 25.0);
    //  var orderY = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 1.0, 15.0, 15.0, null, 5.0);

    //  order.Transaction.Descriptor.Id = orderY.Transaction.Descriptor.Id;
    //  order.Orders.Add(SL);

    //  var orderId = order.Transaction.Descriptor.Id;
    //  var orderIdX = orderX.Transaction.Descriptor.Id;
    //  var orderIdY = orderY.Transaction.Descriptor.Id;

    //  Account.ActiveOrders.Add(orderIdX, orderX);
    //  Account.ActiveOrders.Add(orderIdY, orderY);

    //  base.UpdateOrders(order);

    //  var update = Account.ActiveOrders[orderIdY];

    //  Assert.Equal(2, Account.ActiveOrders.Count);
    //  Assert.Equal(orderIdY, Account.ActiveOrders[orderIdY].Transaction.Descriptor.Id);
    //  Assert.Equal(orderIdX, Account.ActiveOrders[orderIdX].Transaction.Descriptor.Id);
    //  Assert.Equal(orderId, update.Transaction.Descriptor.Id);
    //  Assert.Equal(order.Transaction.Descriptor.Name, update.Transaction.Descriptor.Name);
    //  Assert.Equal(order.Transaction.Descriptor.Description, update.Transaction.Descriptor.Description);
    //  Assert.Equal(order.Transaction.Descriptor.Group, update.Transaction.Descriptor.Group);
    //  Assert.Equal(order.Type, update.Type);
    //  Assert.Equal(order.Side, update.Side);
    //  Assert.Equal(order.Transaction.Volume, update.Transaction.Volume);
    //  Assert.Equal(order.Transaction.Price, update.Transaction.Price);
    //  Assert.Equal(order.Transaction.Instrument, update.Transaction.Instrument);
    //  Assert.Equal(order.ActivationPrice, update.ActivationPrice);
    //  Assert.Equal(order.Orders, update.Orders);
    //  Assert.Empty(Account.ActivePositions);
    //  Assert.Empty(Account.Positions);
    //  Assert.Empty(Account.Orders);
    //}

    //[Fact]
    //public void IncreasePositionWithMatches()
    //{
    //  var instrumentX = Account.Instruments[AssetX];
    //  var instrumentY = Account.Instruments[AssetY];
    //  var pointX = instrumentX.Points.Last();
    //  var pointY = instrumentY.Points.Last();
    //  var orderX = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 1, pointX.Bid, pointX.Ask, null, null);
    //  var orderY = GenerateOrder(AssetY, OrderSideEnum.Buy, OrderTypeEnum.Market, 2, pointY.Bid, pointY.Ask, null, null);
    //  var increase = GenerateOrder(AssetY, OrderSideEnum.Buy, OrderTypeEnum.Market, 3, pointY.Bid + 5, pointY.Ask + 5, null, null);

    //  // Open

    //  base.CreateOrders(orderX);
    //  base.CreateOrders(orderY);

    //  // Increase

    //  var previousPosition = Account.ActivePositions[orderY.Transaction.Descriptor.Id];
    //  var nextPosition = base.IncreasePosition(increase, previousPosition);
    //  var averageTradePrice = nextPosition.Order.Orders.Sum(o => o.Transaction.Volume * o.Transaction.Price) / nextPosition.Orders.Sum(o => o.Transaction.Volume);

    //  Assert.Equal(3, Account.Orders.Count);
    //  Assert.Equal(0, Account.ActiveOrders.Count);
    //  Assert.Equal(2, Account.ActivePositions.Count);
    //  Assert.Empty(Account.Positions);

    //  var openA = nextPosition.Orders[0];
    //  var openB = nextPosition.Orders[1];

    //  Assert.Equal(orderY.Transaction.Volume, openA.Transaction.Volume);
    //  Assert.Equal(increase.Transaction.Volume, openB.Transaction.Volume);
    //  Assert.Equal(pointY.Ask, openA.Transaction.Price);
    //  Assert.Equal(pointY.Ask + 5, openB.Transaction.Price);
    //  Assert.Equal(orderY.Transaction.Time, openA.Transaction.Time);
    //  Assert.Equal(increase.Transaction.Time, openB.Transaction.Time);
    //  Assert.Equal(2, nextPosition.Orders.Count);

    //  Assert.Equal(increase.Transaction.Descriptor.Id, nextPosition.Order.Transaction.Descriptor.Id);
    //  Assert.Equal(increase.Transaction.Time, nextPosition.Order.Transaction.Time);
    //  Assert.Equal(averageTradePrice, nextPosition.Order.Transaction.Price);
    //  Assert.Equal(increase.Transaction.Volume + orderY.Transaction.Volume, nextPosition.Order.Transaction.Volume);

    //  Assert.Equal(nextPosition.Order.Transaction.Time, previousPosition.Order.Transaction.Time);
    //  Assert.Equal(openB.Transaction.Price, previousPosition.Order.Transaction.Price);
    //  Assert.Equal(previousPosition.GainLossEstimate, previousPosition.GainLoss);
    //  Assert.Equal(previousPosition.GainLossPointsEstimate, previousPosition.GainLossPoints);

    //  // Estimate

    //  var step = instrumentY.StepValue / instrumentY.StepSize;
    //  var priceUpdate = new PointModel { Ask = 50, Bid = 40, Last = 40, Instrument = instrumentY };

    //  instrumentY.Points.Add(priceUpdate);
    //  instrumentY.PointGroups.Add(priceUpdate);

    //  nextPosition.Order.Transaction.Instrument = instrumentY;

    //  Assert.Equal(nextPosition.GainLossPointsEstimate * nextPosition.Order.Transaction.Volume * step - instrumentY.Commission, nextPosition.GainLossEstimate);
    //  Assert.Equal(priceUpdate.Bid - nextPosition.Order.Transaction.Price, nextPosition.GainLossPointsEstimate);
    //}

    //[Fact]
    //public void DecreasePositionWithMatches()
    //{
    //  var instrumentX = Account.Instruments[AssetX];
    //  var instrumentY = Account.Instruments[AssetY];
    //  var pointX = instrumentX.Points.Last();
    //  var pointY = instrumentY.Points.Last();
    //  var orderX = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 1, pointX.Bid, pointX.Ask, null, null);
    //  var orderY = GenerateOrder(AssetY, OrderSideEnum.Buy, OrderTypeEnum.Market, 1, pointY.Bid, pointY.Ask, null, null);
    //  var decreaseA = GenerateOrder(AssetY, OrderSideEnum.Sell, OrderTypeEnum.Market, 2, pointY.Bid + 5, pointY.Ask + 5, null, null);
    //  var decreaseB = GenerateOrder(AssetY, OrderSideEnum.Buy, OrderTypeEnum.Market, 1, pointY.Bid + 15, pointY.Ask + 15, null, null);

    //  // Open

    //  base.CreateOrders(orderX);
    //  base.CreateOrders(orderY);

    //  // Inverse

    //  var previousPosition = Account.ActivePositions[orderY.Transaction.Descriptor.Id];
    //  var nextPosition = base.DecreasePosition(decreaseA, previousPosition);

    //  Assert.Equal(3, Account.Orders.Count);
    //  Assert.Equal(0, Account.ActiveOrders.Count);
    //  Assert.Equal(2, Account.ActivePositions.Count);
    //  Assert.Single(Account.Positions);

    //  var openA = nextPosition.Orders[0];

    //  Assert.Equal(decreaseA.Transaction.Volume, openA.Transaction.Volume);
    //  Assert.Equal(pointY.Bid + 5, openA.Transaction.Price);
    //  Assert.Equal(decreaseA.Transaction.Time, openA.Transaction.Time);
    //  Assert.Single(nextPosition.Orders);

    //  Assert.Equal(decreaseA.Transaction.Descriptor.Id, nextPosition.Order.Transaction.Descriptor.Id);
    //  Assert.Equal(decreaseA.Transaction.Time, nextPosition.Order.Transaction.Time);
    //  Assert.Equal(openA.Transaction.Price, nextPosition.Order.Transaction.Price);
    //  Assert.Equal(Math.Abs(orderY.Transaction.Volume.Value - decreaseA.Transaction.Volume.Value), nextPosition.Order.Transaction.Volume);

    //  Assert.Equal(nextPosition.Order.Transaction.Time, previousPosition.Order.Transaction.Time);
    //  Assert.Equal(openA.Transaction.Price, previousPosition.Order.Transaction.Price);
    //  Assert.Equal(previousPosition.GainLossEstimate, previousPosition.GainLoss);
    //  Assert.Equal(previousPosition.GainLossPointsEstimate, previousPosition.GainLossPoints);

    //  // Close

    //  previousPosition = Account.ActivePositions[decreaseA.Transaction.Descriptor.Id];
    //  nextPosition = base.DecreasePosition(decreaseB, previousPosition);

    //  Assert.Equal(4, Account.Orders.Count);
    //  Assert.Equal(0, Account.ActiveOrders.Count);
    //  Assert.Equal(2, Account.Positions.Count);
    //  Assert.Equal(1, Account.ActivePositions.Count);
    //}
  }
}
