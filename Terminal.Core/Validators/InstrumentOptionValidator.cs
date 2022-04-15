using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class InstrumentOptionValidator : AbstractValidator<IInstrumentOptionModel>
  {
    public InstrumentOptionValidator()
    {
      RuleFor(o => o.Side).NotNull().WithMessage("No side");
      RuleFor(o => o.Strike).NotNull().NotEqual(0).WithMessage("No strike");
      RuleFor(o => o.ExpirationDate).NotNull().WithMessage("No expiration date");
    }
  }
}
