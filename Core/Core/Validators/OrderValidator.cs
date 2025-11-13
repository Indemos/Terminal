using Core.Enums;
using Core.Models;
using FluentValidation;
using System.Linq;

namespace Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class OrderValidator : AbstractValidator<Order>
  {
    public OrderValidator(int iteration = 0, int max = 5)
    {
      RuleFor(o => o.Id).NotEmpty();

      When(o => o.Instruction is InstructionEnum.Brace, () =>
      {
        RuleFor(o => o.Orders).Empty();
      });

      When(o => o.Instruction is InstructionEnum.Brace || o.Orders.Count is 0, () =>
      {
        RuleFor(o => o.Type).NotEmpty();
        RuleFor(o => o.Side).NotEmpty();
        RuleFor(o => o.Amount).GreaterThan(0);
        RuleFor(o => o.Operation).NotEmpty().SetValidator(new OperationValidator());

        When(o => o.Operation.Instrument.Price is not null, () => RuleFor(o => o).SetValidator(new OrderPriceValidator()));
      });

      When(o => o.Orders.Count is not 0, () =>
      {
        RuleFor(o => o.Orders.Where(o => Equals(o.Instruction, InstructionEnum.Brace)).All(order => Equals(o.Operation.Instrument.Name, order.Operation.Instrument.Name)));

        if (iteration < max)
        {
          RuleForEach(o => o.Orders).SetValidator(new OrderValidator(iteration + 1));
        }
      });

      When(o => o.Type is OrderTypeEnum.Market, () =>
      {
        RuleFor(o => o.Price).Empty();
        RuleFor(o => o.ActivationPrice).Empty();
      });
    }
  }
}
