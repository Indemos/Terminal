using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class BarValidator : AbstractValidator<BarModel?>
  {
    public BarValidator()
    {
      RuleFor(o => o.Value).NotEmpty().SetValidator(new BarValueValidator());
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class BarValueValidator : AbstractValidator<BarModel>
  {
    public BarValueValidator()
    {
      RuleFor(o => o.Low).NotEmpty();
      RuleFor(o => o.High).NotEmpty();
      RuleFor(o => o.Open).NotEmpty();
      RuleFor(o => o.Close).NotEmpty();
    }
  }
}
