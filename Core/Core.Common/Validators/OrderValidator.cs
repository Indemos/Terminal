using Core.Common.Enums;
using Core.Common.Models;
using FluentValidation;

namespace Core.Common.Validators
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
        RuleFor(o => o.Amount).GreaterThan(0);
        RuleFor(o => o.Operation).NotEmpty();
      });

      When(o => o.Operation is not null, () =>
      {
        RuleFor(o => o.Operation.Amount).Empty();
      });

      When(o => o?.Instruction is null or InstructionEnum.Side or InstructionEnum.Brace, () =>
      {
        RuleFor(o => o.Operation).SetValidator(new TransactionValidator());
      });

      When(o => o.Type is OrderTypeEnum.Market, () =>
      {
        RuleFor(o => o.Price).Empty();
        RuleFor(o => o.ActivationPrice).Empty();
      });

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
