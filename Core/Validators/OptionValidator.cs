using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class OptionValidator : AbstractValidator<DerivativeModel>
  {
    public OptionValidator()
    {
      RuleFor(o => o.Side).NotEmpty();
      RuleFor(o => o.Strike).NotEmpty();
      RuleFor(o => o.Expiration).NotEmpty();
      RuleFor(o => o.Contract).NotEmpty().SetValidator(new PointValidator());
    }
  }
}
