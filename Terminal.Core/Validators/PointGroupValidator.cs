using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointGroupValidator : AbstractValidator<IPointGroupModel>
  {
    public PointGroupValidator()
    {
      RuleFor(o => o.Low).NotNull().NotEqual(0).WithMessage("No low price");
      RuleFor(o => o.High).NotNull().NotEqual(0).WithMessage("No high proce");
      RuleFor(o => o.Open).NotNull().NotEqual(0).WithMessage("No open price");
      RuleFor(o => o.Close).NotNull().NotEqual(0).WithMessage("No close price");
    }
  }
}
