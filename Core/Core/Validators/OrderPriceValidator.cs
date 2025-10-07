using Core.Enums;
using Core.Models;
using FluentValidation;

namespace Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class OrderPriceValidator : AbstractValidator<OrderModel>
  {
    public OrderPriceValidator()
    {
      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.Stop, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Operation.Instrument.Price.Ask);
        RuleFor(o => o.ActivationPrice).Empty();
      });

      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.Stop, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Operation.Instrument.Price.Bid);
        RuleFor(o => o.ActivationPrice).Empty();
      });

      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.Limit, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Operation.Instrument.Price.Ask);
        RuleFor(o => o.ActivationPrice).Empty();
      });

      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.Limit, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Operation.Instrument.Price.Bid);
        RuleFor(o => o.ActivationPrice).Empty();
      });

      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.StopLimit, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.ActivationPrice);
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).GreaterThanOrEqualTo(o => o.Operation.Instrument.Price.Ask);
      });

      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.StopLimit, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).LessThanOrEqualTo(o => o.ActivationPrice);
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).LessThanOrEqualTo(o => o.Operation.Instrument.Price.Bid);
      });
    }
  }
}
