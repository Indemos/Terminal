using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class TransactionPositionGainLossValidator : AbstractValidator<ITransactionPositionModel>
  {
    public TransactionPositionGainLossValidator()
    {
      Include(new TransactionPositionValidator());

      RuleFor(o => o.GainLoss).NotEmpty();
      RuleFor(o => o.GainLossPoints).NotEmpty();
    }
  }
}
