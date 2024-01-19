using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointCollectionValidator : AbstractValidator<PointModel>
  {
    public PointCollectionValidator()
    {
      Include(new PointVolumeValidator());

      RuleFor(o => o.Series).NotEmpty();
    }
  }
}
