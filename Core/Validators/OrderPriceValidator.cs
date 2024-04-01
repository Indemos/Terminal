using FluentValidation;
using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class OrderPriceValidator : AbstractValidator<OrderModel?>
  {
    public OrderPriceValidator()
    {
      RuleFor(o => o.Value).NotEmpty().SetValidator(new OrderPriceValueValidator());
    }
  }

  /// <summary>
  /// Validation rules for limit orders
  /// </summary>
  public class OrderPriceValueValidator : AbstractValidator<OrderModel>
  {
    protected static readonly Dictionary<OrderTypeEnum?, bool> _orderTypes = new()
    {
      [OrderTypeEnum.None] = true,
      [OrderTypeEnum.Market] = true
    };

    public OrderPriceValueValidator()
    {
      Include(new OrderValueValidator());

      When(o => _orderTypes.ContainsKey(o.Type ?? OrderTypeEnum.None) is false, () => RuleFor(o => o.Transaction.Value.Price).NotEmpty());
      When(o => o.Side is OrderSideEnum.Buy && o.Type is OrderTypeEnum.Stop, () => RuleFor(o => o.Transaction.Value.Price).GreaterThanOrEqualTo(o => o.Transaction.Value.Instrument.Points.Last().Value.Ask));
      When(o => o.Side is OrderSideEnum.Sell && o.Type is OrderTypeEnum.Stop, () => RuleFor(o => o.Transaction.Value.Price).LessThanOrEqualTo(o => o.Transaction.Value.Instrument.Points.Last().Value.Bid));
      When(o => o.Side is OrderSideEnum.Buy && o.Type is OrderTypeEnum.Limit, () => RuleFor(o => o.Transaction.Value.Price).LessThanOrEqualTo(o => o.Transaction.Value.Instrument.Points.Last().Value.Ask));
      When(o => o.Side is OrderSideEnum.Sell && o.Type is OrderTypeEnum.Limit, () => RuleFor(o => o.Transaction.Value.Price).GreaterThanOrEqualTo(o => o.Transaction.Value.Instrument.Points.Last().Value.Bid));

      When(o => Equals(o.Side, OrderSideEnum.Buy) && Equals(o.Type, OrderTypeEnum.StopLimit), () =>
      {
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).GreaterThanOrEqualTo(o => o.Transaction.Value.Instrument.Points.Last().Value.Ask);
        RuleFor(o => o.Transaction.Value.Price).GreaterThanOrEqualTo(o => o.ActivationPrice);
      });

      When(o => Equals(o.Side, OrderSideEnum.Sell) && Equals(o.Type, OrderTypeEnum.StopLimit), () =>
      {
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).LessThanOrEqualTo(o => o.Transaction.Value.Instrument.Points.Last().Value.Bid);
        RuleFor(o => o.Transaction.Value.Price).LessThanOrEqualTo(o => o.ActivationPrice);
      });
    }
  }
}
