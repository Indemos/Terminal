using FluentValidation;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules for limit orders
  /// </summary>
  public class OrderPriceValidator : AbstractValidator<OrderModel>
  {
    public OrderPriceValidator()
    {
      Include(new OrderValidator());

      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.Stop, () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Transaction.Instrument.Point.Ask));
      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.Stop, () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Transaction.Instrument.Point.Bid));
      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.Limit, () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Transaction.Instrument.Point.Ask));
      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.Limit, () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Transaction.Instrument.Point.Bid));

      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.StopLimit, () =>
      {
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).GreaterThanOrEqualTo(o => o.Transaction.Instrument.Point.Ask);
        RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.ActivationPrice);
      });

      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.StopLimit, () =>
      {
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).LessThanOrEqualTo(o => o.Transaction.Instrument.Point.Bid);
        RuleFor(o => o.Price).LessThanOrEqualTo(o => o.ActivationPrice);
      });
    }
  }
}
