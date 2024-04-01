using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointValidator : AbstractValidator<PointModel?>
  {
    public PointValidator()
    {
      RuleFor(o => o.Value).NotEmpty().SetValidator(new PointValueValidator());
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointValueValidator : AbstractValidator<PointModel>
  {
    public PointValueValidator()
    {
      RuleFor(o => o.Bid).NotEmpty();
      RuleFor(o => o.Ask).NotEmpty();
      RuleFor(o => o.Price).NotEmpty();
      RuleFor(o => o.Time).NotEmpty();
      RuleFor(o => o.Instrument).NotEmpty().SetValidator(new InstrumentValidator());
    }
  }
}
