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

      RuleFor(o => o.OpenPrice).NotEmpty();
      RuleFor(o => o.ClosePrice).NotEmpty();
      RuleFor(o => o.OpenPrices).NotEmpty();
    }
  }
}
