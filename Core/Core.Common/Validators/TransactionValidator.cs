using Core.Common.States;
using FluentValidation;

namespace Core.Common.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class TransactionValidator : AbstractValidator<OperationState>
  {
    public TransactionValidator()
    {
      RuleFor(o => o.Id).Empty();
      RuleFor(o => o.Price).Empty();
      RuleFor(o => o.Amount).Empty();
      RuleFor(o => o.Status).Empty();
      RuleFor(o => o.AveragePrice).Empty();
      RuleFor(o => o.Instrument).NotEmpty().SetValidator(new InstrumentValidator());
    }
  }
}
