using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointVolumeValidator : AbstractValidator<PointModel?>
  {
    public PointVolumeValidator()
    {
      RuleFor(o => o.Value).NotEmpty().SetValidator(new PointValueValidator());
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointVolumeValueValidator : AbstractValidator<PointModel>
  {
    public PointVolumeValueValidator()
    {
      Include(new PointValueValidator());

      RuleFor(o => o.BidSize).NotEmpty();
      RuleFor(o => o.AskSize).NotEmpty();
    }
  }
}
