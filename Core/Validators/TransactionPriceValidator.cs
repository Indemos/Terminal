using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class TransactionPriceValidator : AbstractValidator<TransactionModel>
  {
    public TransactionPriceValidator()
    {
      Include(new TransactionValidator());

      RuleFor(o => o.Price).NotEmpty();
    }
  }
}
