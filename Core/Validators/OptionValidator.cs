using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class OptionValidator : AbstractValidator<OptionModel>
  {
    public OptionValidator()
    {
      RuleFor(o => o.Name).NotEmpty();
      RuleFor(o => o.BaseName).NotEmpty();
      RuleFor(o => o.Side).NotEmpty();
      RuleFor(o => o.Strike).NotEmpty();
      RuleFor(o => o.Leverage).NotEmpty();
      RuleFor(o => o.ExpirationDate).NotEmpty();
      RuleFor(o => o.Instrument).NotEmpty().SetValidator(new InstrumentValidator());
    }
  }
}
