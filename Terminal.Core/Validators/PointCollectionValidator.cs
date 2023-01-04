using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointCollectionValidator : AbstractValidator<IPointModel>
  {
    public PointCollectionValidator()
    {
      Include(new PointValidator());

      RuleFor(o => o.Series).NotNull();
    }
  }
}
