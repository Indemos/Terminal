using System;
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
      var span = TimeSpan.FromSeconds(1);

      Account = new AccountModel
      {
        Name = "Demo",
        Balance = 50000,
        Instruments = new NameCollection<string, IInstrumentModel>
        {
          [AssetX] = new InstrumentModel { Name = AssetX, TimeFrame = span },
          [AssetY] = new InstrumentModel { Name = AssetY, TimeFrame = span }
        }
      };
    }

    public static ITransactionOrderModel GenerateOrder(
      string asset,
      OrderSideEnum? orderSide,
      OrderTypeEnum? orderType,
      double? volume,
      double? price,
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
        Ask = price,
        Bid = price,
        Last = price,
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
    public void BreakValidationOnEmptyOrder()
    {
      var instrument = new InstrumentModel();
      var order = new TransactionOrderModel();
      var error = "NotEmptyValidator";
      var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

      Assert.Contains($"{nameof(order.Side)} {error}", errors);
      Assert.Contains($"{nameof(order.Volume)} {error}", errors);
      Assert.Contains($"{nameof(order.Type)} {error}", errors);
      Assert.Contains($"{nameof(instrument.Name)} {error}", errors);
      Assert.Contains($"{nameof(instrument.TimeFrame)} {error}", errors);
      Assert.Contains($"{nameof(instrument.Points)} {error}", errors);
      Assert.Contains($"{nameof(instrument.PointGroups)} {error}", errors);
    }

    [Fact]
    public void BreakValidationOnOrderWithoutQuote()
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
      Assert.Contains($"{nameof(instrument.TimeFrame)} {error}", errors);
      Assert.Contains($"{nameof(point.Bid)} {error}", errors);
      Assert.Contains($"{nameof(point.Ask)} {error}", errors);
      Assert.Contains($"{nameof(point.Last)} {error}", errors);
    }

    [Theory]
    [InlineData(OrderTypeEnum.Stop)]
    [InlineData(OrderTypeEnum.Limit)]
    [InlineData(OrderTypeEnum.StopLimit)]
    public void BreakValidationOnPendingOrderWithoutPrice(OrderTypeEnum orderType)
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
    public void BreakValidationOnPendingOrderWithIncorrectPrice(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double orderPrice,
      double price,
      string error)
    {
      var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, price, null, orderPrice);
      var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

      Assert.Contains($"{nameof(order.Price)} {error}", errors);
    }

    [Theory]
    [InlineData(OrderSideEnum.Buy, 15.0, null, 10.0, "NotEmptyValidator", "GreaterThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, 15.0, null, 5.0, "NotEmptyValidator", "LessThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Buy, 5.0, 10.0, 15.0, "GreaterThanOrEqualValidator", "GreaterThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, 15.0, 10.0, 5.0, "LessThanOrEqualValidator", "LessThanOrEqualValidator")]
    public void BreakValidationOnStopLimitOrderWithIncorrectPrice(
      OrderSideEnum orderSide,
      double? orderPrice,
      double? activationPrice,
      double? price,
      string activationError,
      string orderError)
    {
      var order = GenerateOrder(AssetX, orderSide, OrderTypeEnum.StopLimit, 1.0, price, activationPrice, orderPrice);
      var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

      Assert.Contains($"{nameof(order.ActivationPrice)} {activationError}", errors);
      Assert.Contains($"{nameof(order.Price)} {orderError}", errors);
    }

    [Fact]
    public void SkipIncorrectMarketOrder()
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
    public void RegisterOrderWithoutMatching(
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
      var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, price, activationPrice, orderPrice);

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
    public void RegisterPendingOrder(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? price,
      double? activationPrice,
      double? orderPrice)
    {
      var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, price, activationPrice, orderPrice);
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
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Market, 15.0)]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Market, 15.0)]
    public void CreatePositionWithoutMatching(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double? price)
    {
      var order = GenerateOrder(AssetX, orderSide, orderType, 1.0, price, null, null);
      var point = order.Instrument.Points.First();
      var orderId = order.Id;

      base.CreatePosition(order);

      var position = Account.ActivePositions[orderId];

      Assert.Equal(order.Time, point.Time);
      Assert.Equal(order.Price, price);
      Assert.Equal(order.Status, OrderStatusEnum.Filled);

      Assert.Equal(position.Time, point.Time);
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
      var order = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 1.0, price, null, null);
      var SL = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 1.0, price, null, 5.0);
      var TP = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Limit, 1.0, price, null, 25.0);
      var orderId = order.Id;
      var SLID = SL.Id;
      var TPID = TP.Id;

      order.Orders.Add(SL);
      order.Orders.Add(TP);

      base.CreatePosition(order);

      Assert.Equal(order, Account.Orders[0]);
      Assert.Equal(SL, Account.Orders[1]);
      Assert.Equal(TP, Account.Orders[2]);
      Assert.Equal(SL, Account.ActiveOrders[SLID]);
      Assert.Equal(TP, Account.ActiveOrders[TPID]);
      Assert.Equal(orderId, Account.ActivePositions[orderId].Id);
      Assert.Equal(3, Account.Orders.Count);
      Assert.Equal(2, Account.ActiveOrders.Count);
      Assert.Single(Account.ActivePositions);
      Assert.Empty(Account.Positions);
    }

    [Fact]
    public void CopyTransaction()
    {
      var SL = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 2.0, 15.0, 5.0, null);
      var order = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 1.0, 15.0, null, 25.0);
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
    public void UpdateOrderProps()
    {
      var SL = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 2.0, 15.0, 5.0, null);
      var order = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 2.0, 5.0, 10.0, 15.0);
      var orderX = GenerateOrder(AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 1.0, 15.0, null, 25.0);
      var orderY = GenerateOrder(AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 1.0, 15.0, null, 5.0);

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

    [Theory]
    [InlineData(2.0,
      AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 15.0, null, null,
      AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 15.0, null, null, 2, 0, 0, 1)]
    [InlineData(2.0,
      AssetX, OrderSideEnum.Sell, OrderTypeEnum.Market, 15.0, null, null,
      AssetX, OrderSideEnum.Sell, OrderTypeEnum.Market, 15.0, null, null, 2, 0, 0, 1)]
    [InlineData(2.0,
      AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 15.0, null, 25.0,
      AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 15.0, null, 25.0, 2, 2, 0, 0)]
    [InlineData(2.0,
      AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 5.0,
      AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 5.0, 2, 2, 0, 0)]
    [InlineData(2.0,
      AssetX, OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 5.0,
      AssetX, OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 5.0, 2, 2, 0, 0)]
    [InlineData(2.0,
      AssetX, OrderSideEnum.Sell, OrderTypeEnum.Limit, 15.0, null, 25.0,
      AssetX, OrderSideEnum.Sell, OrderTypeEnum.Limit, 15.0, null, 25.0, 2, 2, 0, 0)]
    [InlineData(2.0,
      AssetX, OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0,
      AssetX, OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0, 2, 2, 0, 0)]
    [InlineData(2.0,
      AssetX, OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0,
      AssetX, OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0, 2, 2, 0, 0)]
    public void Increase(
      double volumeX,
      string assetX,
      OrderSideEnum orderSideX,
      OrderTypeEnum orderTypeX,
      double? priceX,
      double? activationPriceX,
      double? orderPriceX,
      string assetY,
      OrderSideEnum orderSideY,
      OrderTypeEnum orderTypeY,
      double? priceY,
      double? activationPriceY,
      double? orderPriceY,
      int orders,
      int activeOrders,
      int positions,
      int activePositions)
    {
      var orderX = GenerateOrder(assetX, orderSideX, orderTypeX, volumeX, priceX, activationPriceX, orderPriceX);
      var orderY = GenerateOrder(assetY, orderSideY, orderTypeY, 2.0, priceY, activationPriceY, orderPriceY);

      base.CreateOrders(orderX);
      base.CreateOrders(orderY);

      Assert.Equal(orders, Account.Orders.Count);
      Assert.Equal(activeOrders, Account.ActiveOrders.Count);
      Assert.Equal(positions, Account.Positions.Count);
      Assert.Equal(activePositions, Account.ActivePositions.Count);
    }

    //// Same volume - same side - different assets - same type

    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 15.0, null, null,
    //  AssetY, OrderSideEnum.Buy, OrderTypeEnum.Market, 15.0, null, null, 2, 0, 0, 2)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Market, 15.0, null, null,
    //  AssetY, OrderSideEnum.Sell, OrderTypeEnum.Market, 15.0, null, null, 2, 0, 0, 2)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 15.0, null, 25.0,
    //  AssetY, OrderSideEnum.Buy, OrderTypeEnum.Stop, 15.0, null, 25.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 5.0,
    //  AssetY, OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 5.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 5.0,
    //  AssetY, OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 5.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Limit, 15.0, null, 25.0,
    //  AssetY, OrderSideEnum.Sell, OrderTypeEnum.Limit, 15.0, null, 25.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0,
    //  AssetY, OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0,
    //  AssetY, OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0, 2, 2, 0, 0)]

    //// Same volume - different sides - same asset - same type

    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 15.0, null, null,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Market, 15.0, null, null, 2, 0, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Market, 15.0, null, null,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 15.0, null, null, 2, 0, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 15.0, null, 25.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 25.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 5.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 15.0, null, 5.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 5.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Limit, 15.0, null, 5.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Limit, 15.0, null, 25.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 25.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0, 2, 2, 0, 0)]

    //// Same volume - different sides - different assets - same type

    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Market, 15.0, null, null,
    //  AssetY, OrderSideEnum.Sell, OrderTypeEnum.Market, 15.0, null, null, 2, 0, 0, 2)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Market, 15.0, null, null,
    //  AssetY, OrderSideEnum.Buy, OrderTypeEnum.Market, 15.0, null, null, 2, 0, 0, 2)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Stop, 15.0, null, 25.0,
    //  AssetY, OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 25.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Stop, 15.0, null, 5.0,
    //  AssetY, OrderSideEnum.Buy, OrderTypeEnum.Stop, 15.0, null, 5.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 5.0,
    //  AssetY, OrderSideEnum.Sell, OrderTypeEnum.Limit, 15.0, null, 5.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.Limit, 15.0, null, 25.0,
    //  AssetY, OrderSideEnum.Buy, OrderTypeEnum.Limit, 15.0, null, 25.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0,
    //  AssetY, OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 20.0, 25.0, 2, 2, 0, 0)]
    //[InlineData(2.0,
    //  AssetX, OrderSideEnum.Sell, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0,
    //  AssetY, OrderSideEnum.Buy, OrderTypeEnum.StopLimit, 15.0, 10.0, 5.0, 2, 2, 0, 0)]

    //public void RegisterOrderWithMatching(
    //  double volumeX,
    //  string assetX,
    //  OrderSideEnum orderSideX,
    //  OrderTypeEnum orderTypeX,
    //  double? priceX,
    //  double? activationPriceX,
    //  double? orderPriceX,
    //  string assetY,
    //  OrderSideEnum orderSideY,
    //  OrderTypeEnum orderTypeY,
    //  double? priceY,
    //  double? activationPriceY,
    //  double? orderPriceY,
    //  int orders,
    //  int activeOrders,
    //  int positions,
    //  int activePositions)
    //{
    //  var orderX = GenerateOrder(assetX, orderSideX, orderTypeX, volumeX, priceX, activationPriceX, orderPriceX);
    //  var orderY = GenerateOrder(assetY, orderSideY, orderTypeY, 2.0, priceY, activationPriceY, orderPriceY);

    //  base.CreateOrders(orderX);
    //  base.CreateOrders(orderY);

    //  Assert.Equal(orders, Account.Orders.Count);
    //  Assert.Equal(activeOrders, Account.ActiveOrders.Count);
    //  Assert.Equal(positions, Account.Positions.Count);
    //  Assert.Equal(activePositions, Account.ActivePositions.Count);
    //}
  }
}
