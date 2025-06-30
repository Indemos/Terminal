using FluentValidation;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class OrderValidator : AbstractValidator<OrderModel>
  {
    public OrderValidator()
    {
      RuleFor(o => o.Price).Empty();
      RuleFor(o => o.Status).Empty();
      RuleFor(o => o.OpenAmount).Empty();
      RuleFor(o => o.Descriptor).NotEmpty();

      When(o => o.Amount is not null, () =>
      {
        RuleFor(o => o.Type).NotEmpty();
        RuleFor(o => o.Side).NotEmpty();
        RuleFor(o => o.Name).NotEmpty();
      });

      // Price

      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.Stop, () =>
      {
        RuleFor(o => o.OpenPrice).NotEmpty();
        RuleFor(o => o.OpenPrice).GreaterThanOrEqualTo(o => o.Instrument.Point.Ask);
      });

      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.Stop, () =>
      {
        RuleFor(o => o.OpenPrice).NotEmpty();
        RuleFor(o => o.OpenPrice).LessThanOrEqualTo(o => o.Instrument.Point.Bid);
      });

      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.Limit, () =>
      {
        RuleFor(o => o.OpenPrice).NotEmpty();
        RuleFor(o => o.OpenPrice).LessThanOrEqualTo(o => o.Instrument.Point.Ask);
      });

      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.Limit, () =>
      {
        RuleFor(o => o.OpenPrice).NotEmpty();
        RuleFor(o => o.OpenPrice).GreaterThanOrEqualTo(o => o.Instrument.Point.Bid);
      });

      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.StopLimit, () =>
      {
        RuleFor(o => o.OpenPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.OpenPrice).GreaterThanOrEqualTo(o => o.ActivationPrice);
        RuleFor(o => o.ActivationPrice).GreaterThanOrEqualTo(o => o.Instrument.Point.Ask);
      });

      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.StopLimit, () =>
      {
        RuleFor(o => o.OpenPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.OpenPrice).LessThanOrEqualTo(o => o.ActivationPrice);
        RuleFor(o => o.ActivationPrice).LessThanOrEqualTo(o => o.Instrument.Point.Bid);
      });
    }
  }
}
