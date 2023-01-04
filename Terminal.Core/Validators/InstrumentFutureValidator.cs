using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class InstrumentFutureValidator : AbstractValidator<IInstrumentFutureModel>
  {
    public InstrumentFutureValidator()
    {
      RuleFor(o => o.ExpirationDate).NotEmpty();
    }
  }
}
