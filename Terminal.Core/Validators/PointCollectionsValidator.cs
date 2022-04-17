using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointCollectionsValidator : AbstractValidator<IPointModel>
  {
    public PointCollectionsValidator()
    {
      Include(new PointValidator());

      RuleFor(o => o.Groups).NotNull().NotEmpty().WithMessage("No series");
    }
  }
}
