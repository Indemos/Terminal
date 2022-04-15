using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class TimeValidator : AbstractValidator<ITimeModel>
  {
    public TimeValidator()
    {
      RuleFor(o => o.Last).NotNull().NotEqual(0).WithMessage("No last price");
      RuleFor(o => o.Time).NotNull().WithMessage("No time");
      RuleFor(o => o.TimeFrame).NotNull().WithMessage("No time frame");
    }
  }
}
