using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class InstrumentValidator : AbstractValidator<IInstrumentModel>
  {
    public InstrumentValidator()
    {
      RuleFor(o => o.Name).NotEmpty();
      RuleFor(o => o.SwapLong).NotEmpty();
      RuleFor(o => o.SwapShort).NotEmpty();
      RuleFor(o => o.Commission).NotEmpty();
      RuleFor(o => o.ContractSize).NotEmpty();
      RuleFor(o => o.StepSize).NotEmpty();
      RuleFor(o => o.StepValue).NotEmpty();
    }
  }
}
