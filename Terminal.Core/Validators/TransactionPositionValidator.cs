using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class TransactionPositionValidator : AbstractValidator<ITransactionPositionModel>
  {
    public TransactionPositionValidator()
    {
      Include(new TransactionOrderValidator());

      RuleFor(o => o.OpenPrice).NotNull().WithMessage("No open price");
      RuleFor(o => o.ClosePrice).NotNull().WithMessage("No close price");
      RuleFor(o => o.OpenPrices).NotNull().WithMessage("No open prices");
    }
  }
}
