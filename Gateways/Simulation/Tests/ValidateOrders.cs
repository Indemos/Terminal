using FluentValidation.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Connector.Simulation;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class ValidateOrders : Adapter, IDisposable
  {
    public ValidateOrders()
    {
      Account = new Account
      {
        Name = "Demo",
        Balance = 50000
      };
    }

    [Fact]
    public void ValidateOrdersWithoutProps()
    {
      var order = new OrderModel();
      var error = "NotEmptyValidator";
      var response = base.ValidateOrders(order).Items.First();
      var errors = GetErrors(response.Errors);

      Assert.Equal(4, response.Errors.Count);
      Assert.Contains($"{nameof(order.Side)} {error}", errors);
      Assert.Contains($"{nameof(order.Type)} {error}", errors);
      Assert.Contains($"{nameof(order.TimeSpan)} {error}", errors);
      Assert.Contains($"{nameof(order.Transaction)} {error}", errors);
    }

    [Fact]
    public void ValidateOrdersWithEmptyTransaction()
    {
      var order = new OrderModel
      {
        Transaction = new()
        {
          Id = null,
          Time = null
        }
      };

      var error = "NotEmptyValidator";
      var response = base.ValidateOrders(order).Items.First();
      var errors = GetErrors(response.Errors);

      Assert.Equal(8, response.Errors.Count);
      Assert.Contains($"{nameof(order.Transaction.Id)} {error}", errors);
      Assert.Contains($"{nameof(order.Transaction.Time)} {error}", errors);
      Assert.Contains($"{nameof(order.Transaction.Price)} {error}", errors);
      Assert.Contains($"{nameof(order.Transaction.Volume)} {error}", errors);
      Assert.Contains($"{nameof(order.Transaction.Instrument)} {error}", errors);
    }

    [Fact]
    public void ValidateOrdersWithEmptyInstrument()
    {
      var order = new OrderModel
      {
        Transaction = new()
        {
          Instrument = new Instrument()
          {
            SwapLong = null,
            SwapShort = null,
            StepSize = null,
            StepValue = null,
            Commission = null,
            ContractSize = null
          }
        }
      };

      var error = "NotEmptyValidator";
      var response = base.ValidateOrders(order).Items.First();
      var errors = GetErrors(response.Errors);

      Assert.Equal(10, response.Errors.Count);
      Assert.Contains($"{nameof(order.Transaction.Instrument.Name)} {error}", errors);
      Assert.Contains($"{nameof(order.Transaction.Instrument.Commission)} {error}", errors);
      Assert.Contains($"{nameof(order.Transaction.Instrument.ContractSize)} {error}", errors);
      Assert.Contains($"{nameof(order.Transaction.Instrument.StepSize)} {error}", errors);
      Assert.Contains($"{nameof(order.Transaction.Instrument.StepValue)} {error}", errors);
    }

    [Theory]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Stop, 5.0, 10.0, "GreaterThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Stop, 10.0, 5.0, "LessThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Buy, OrderTypeEnum.Limit, 10.0, 5.0, "LessThanOrEqualValidator")]
    [InlineData(OrderSideEnum.Sell, OrderTypeEnum.Limit, 5.0, 10.0, "GreaterThanOrEqualValidator")]
    public void ValidateOrdersWithIncorrectExecutionPrice(
      OrderSideEnum orderSide,
      OrderTypeEnum orderType,
      double orderPrice,
      double price,
      string error)
    {
      var order = new OrderModel
      {
        Side = orderSide,
        Type = orderType,
        Transaction = new()
        {
          Volume = 1,
          Price = orderPrice,
          Instrument = new Instrument()
          {
            Name = "X",
            Points = new ObservableTimeCollection<PointModel>
            {
              new() { Bid = price, Ask = price }
            }
          }
        }
      };

      var response = base.ValidateOrders(order).Items.First();
      var errors = GetErrors(response.Errors);

      Assert.Contains($"{nameof(order.Transaction.Price)} {error}", errors);
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
      var order = new OrderModel
      {
        Side = orderSide,
        Type = OrderTypeEnum.StopLimit,
        ActivationPrice = activationPrice,
        Transaction = new()
        {
          Volume = 1,
          Price = orderPrice,
          Instrument = new Instrument()
          {
            Name = "X",
            Points = new ObservableTimeCollection<PointModel>
            {
              new() { Bid = price, Ask = price }
            }
          }
        }
      };

      var response = base.ValidateOrders(order).Items.First();
      var errors = GetErrors(response.Errors);

      Assert.Contains($"{nameof(order.ActivationPrice)} {activationError}", errors);
      Assert.Contains($"{nameof(order.Transaction.Price)} {orderError}", errors);
    }

    private IEnumerable<string> GetErrors(IList<ErrorModel> errors)
    {
      return errors.Select(o => $"{o.PropertyName.Split('.').Last()} {o.ErrorCode}");
    }
  }
}
