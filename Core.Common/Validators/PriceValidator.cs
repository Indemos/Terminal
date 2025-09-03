using FluentValidation;
using Core.Common.States;

namespace Core.Common.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PriceValidator : AbstractValidator<PriceState>
  {
    public PriceValidator()
    {
      RuleFor(o => o.Bid).NotEmpty();
      RuleFor(o => o.Ask).NotEmpty();
      RuleFor(o => o.Last).NotEmpty();
      RuleFor(o => o.Time).NotEmpty();

      When(o => o.Bar is not null, () =>
      {
        RuleFor(o => o.Bar.Low).NotEmpty();
        RuleFor(o => o.Bar.High).NotEmpty();
        RuleFor(o => o.Bar.Open).NotEmpty();
        RuleFor(o => o.Bar.Close).NotEmpty();
      });
    }
  }
}
