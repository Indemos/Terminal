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
    const string Asset = "SPY";

    public SimulationTests()
    {
      var span = TimeSpan.FromSeconds(1);

      Account = new AccountModel
      {
        Name = "Demo",
        Balance = 50000,
        Instruments = new NameCollection<string, IInstrumentModel>
        {
          [Asset] = new InstrumentModel { Name = Asset, TimeFrame = span }
        }
      };
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
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Stop, 5, 10, "GreaterThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Stop, 10, 5, "LessThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Limit, 10, 5, "LessThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Limit, 5, 10, "GreaterThanOrEqualValidator")]
    public void BreakValidationOnPendingOrderWithIncorrectPrice(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double orderPrice,
      double price,
      string error)
    {
      var instrument = new InstrumentModel
      {
        Name = Asset,
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
        Volume = 1,
        Side = orderSide,
        Type = orderType,
        Instrument = instrument,
        Price = orderPrice
      };

      instrument.Points.Add(point);
      instrument.PointGroups.Add(point);

      var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

      Assert.Contains($"{nameof(order.Price)} {error}", errors);
    }

    [Theory]
    [InlineData(OrderSideEnum.Buy, null, 15, 10, "NotEmptyValidator", "GreaterThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, null, 15, 5, "NotEmptyValidator", "LessThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Buy, 10.0, 5, 15, "GreaterThanOrEqualValidator", "GreaterThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, 10.0, 15, 5, "LessThanOrEqualValidator", "LessThanOrEqualValidator")]
    public void BreakValidationOnStopLimitOrderWithIncorrectPrice(
      OrderSideEnum orderSide,
      double? activationPrice,
      double orderPrice,
      double price,
      string activationError,
      string orderError)
    {
      var instrument = new InstrumentModel
      {
        Name = Asset,
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
        Volume = 1,
        Side = orderSide,
        Type = OrderTypeEnum.StopLimit,
        Instrument = instrument,
        ActivationPrice = activationPrice,
        Price = orderPrice
      };

      instrument.Points.Add(point);
      instrument.PointGroups.Add(point);

      var errors = base.ValidateOrders(order).Select(o => $"{o.PropertyName} {o.ErrorCode}");

      Assert.Contains($"{nameof(order.ActivationPrice)} {activationError}", errors);
      Assert.Contains($"{nameof(order.Price)} {orderError}", errors);
    }

    [Fact]
    public void CreateMarketOrder()
    {
    }
  }
}
