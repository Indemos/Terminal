using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class TransactionValidator : AbstractValidator<ITransactionModel>
  {
    public TransactionValidator()
    {
      RuleFor(o => o.Instrument).NotNull().NotEmpty().WithMessage("No instrument");
      RuleFor(o => o.Size).NotNull().NotEqual(0).WithMessage("No size");
    }
  }
}
