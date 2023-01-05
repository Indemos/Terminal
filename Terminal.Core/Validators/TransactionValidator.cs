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
      RuleFor(o => o.Instrument).NotEmpty();
      RuleFor(o => o.Volume).NotEmpty();
    }
  }
}
