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

      RuleFor(o => o.GainLoss).NotNull().WithMessage("No PnL");
      RuleFor(o => o.GainLossPoints).NotNull().WithMessage("No PnL points");
    }
  }
}
