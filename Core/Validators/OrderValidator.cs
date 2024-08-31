using FluentValidation;
using System.Linq;
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
      RuleFor(o => o.Type).NotEmpty();

      When(o => Equals(o.Instruction, InstructionEnum.Group) || o.Orders.Where(o => Equals(o.Instruction, InstructionEnum.Side)).Any(), () =>
      {
        RuleFor(o => o.Transaction).Empty();
        RuleFor(o => o.Side).Empty();
      });

      When(o => o.Transaction is not null, () =>
      {
        RuleFor(o => o.Side).NotEmpty();
        RuleFor(o => o.Transaction).SetValidator(new TransactionValidator());
      });
    }
  }
}
