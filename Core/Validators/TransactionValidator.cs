using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class TransactionValidator : AbstractValidator<TransactionModel>
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
