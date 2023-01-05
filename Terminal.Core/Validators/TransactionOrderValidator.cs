using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class TransactionOrderValidator : AbstractValidator<ITransactionOrderModel>
  {
    public TransactionOrderValidator()
    {
      RuleFor(o => o.Instrument).NotEmpty();
      RuleFor(o => o.Side).NotEmpty();
      RuleFor(o => o.Volume).NotEmpty();
      RuleFor(o => o.Type).NotEmpty();
    }
  }
}
