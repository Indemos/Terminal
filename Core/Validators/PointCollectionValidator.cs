using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointCollectionValidator : AbstractValidator<PointModel?>
  {
    public PointCollectionValidator()
    {
      RuleFor(o => o.Value).NotEmpty().SetValidator(new PointCollectionValueValidator());
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointCollectionValueValidator : AbstractValidator<PointModel>
  {
    public PointCollectionValueValidator()
    {
      Include(new PointVolumeValueValidator());

      RuleFor(o => o.Series).NotEmpty();
    }
  }
}
