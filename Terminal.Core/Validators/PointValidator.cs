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
      RuleFor(o => o.Bid).NotNull().NotEqual(0).WithMessage("No bid");
      RuleFor(o => o.Ask).NotNull().NotEqual(0).WithMessage("No offer");
      RuleFor(o => o.BidSize).NotNull().WithMessage("No bid size");
      RuleFor(o => o.AskSize).NotNull().WithMessage("No offer size");
      RuleFor(o => o.Account).NotNull().WithMessage("No account");
      RuleFor(o => o.Instrument).NotNull().WithMessage("No instrument");
      RuleFor(o => o.Groups).NotNull().WithMessage("No series");
    }
  }
}
