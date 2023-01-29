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
      RuleFor(o => o.Last).NotEmpty();
      RuleFor(o => o.Time).NotEmpty();
    }
  }
}
