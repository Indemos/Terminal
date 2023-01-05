using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointValidator : AbstractValidator<IPointModel>
  {
    public PointValidator()
    {
      RuleFor(o => o.Bid).NotEmpty();
      RuleFor(o => o.Ask).NotEmpty();
      RuleFor(o => o.Last).NotEmpty();
      RuleFor(o => o.Account).NotEmpty();
      RuleFor(o => o.Instrument).NotEmpty();
    }
  }
}
