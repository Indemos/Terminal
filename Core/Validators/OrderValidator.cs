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
      RuleFor(o => o.Id).NotEmpty();

      When(o => o.Amount is not null, () =>
      {
        RuleFor(o => o.Type).NotEmpty();
        RuleFor(o => o.Side).NotEmpty();
        RuleFor(o => o.Name).NotEmpty();
      });

      When(o => o.Transaction is not null, () =>
      {
        RuleFor(o => o.Transaction.Amount).Empty();
      });

      When(o => o?.Instruction is null or InstructionEnum.Side or InstructionEnum.Brace, () =>
      {
        RuleFor(o => o.Transaction).SetValidator(new TransactionValidator());
      });

      When(o => o.Type is OrderTypeEnum.Market, () =>
      {
        RuleFor(o => o.Price).Empty();
        RuleFor(o => o.ActivationPrice).Empty();
      });

      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.Stop, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Transaction.Instrument.Point.Ask);
        RuleFor(o => o.ActivationPrice).Empty();
      });

      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.Stop, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Transaction.Instrument.Point.Bid);
        RuleFor(o => o.ActivationPrice).Empty();
      });

      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.Limit, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Transaction.Instrument.Point.Ask);
        RuleFor(o => o.ActivationPrice).Empty();
      });

      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.Limit, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Transaction.Instrument.Point.Bid);
        RuleFor(o => o.ActivationPrice).Empty();
      });

      When(o => o.Side is OrderSideEnum.Long && o.Type is OrderTypeEnum.StopLimit, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.ActivationPrice);
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).GreaterThanOrEqualTo(o => o.Transaction.Instrument.Point.Ask);
      });

      When(o => o.Side is OrderSideEnum.Short && o.Type is OrderTypeEnum.StopLimit, () =>
      {
        RuleFor(o => o.Price).NotEmpty();
        RuleFor(o => o.Price).LessThanOrEqualTo(o => o.ActivationPrice);
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).LessThanOrEqualTo(o => o.Transaction.Instrument.Point.Bid);
      });
    }
  }
}
