using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointVolumeValidator : AbstractValidator<IPointModel>
  {
    public PointVolumeValidator()
    {
      Include(new PointValidator());

      RuleFor(o => o.BidSize).NotEmpty();
      RuleFor(o => o.AskSize).NotEmpty();
    }
  }
}
