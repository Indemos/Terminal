using Core.Enums;
using Core.Models;
using FluentValidation;

namespace Core.Validators
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
        RuleFor(o => o.Operation).SetValidator(new OperationValidator());
      });

      When(o => o.Type is OrderTypeEnum.Market, () =>
      {
        RuleFor(o => o.Price).Empty();
        RuleFor(o => o.ActivationPrice).Empty();
      });

      When(o => o.Operation.Instrument.Price is not null, () => RuleFor(o => o).SetValidator(new OrderPriceValidator()));
    }
  }
}
