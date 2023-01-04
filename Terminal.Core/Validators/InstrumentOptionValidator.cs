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
      RuleFor(o => o.Side).NotEmpty();
      RuleFor(o => o.Strike).NotEmpty();
      RuleFor(o => o.ExpirationDate).NotEmpty();
    }
  }
}
