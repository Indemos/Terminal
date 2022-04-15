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
      RuleFor(o => o.SwapLong).NotNull().WithMessage("No long swap");
      RuleFor(o => o.SwapShort).NotNull().WithMessage("No short swap");
      RuleFor(o => o.Commission).NotNull().WithMessage("No commission");
      RuleFor(o => o.ContractSize).NotNull().NotEqual(0).WithMessage("No contract size");
      RuleFor(o => o.StepSize).NotNull().NotEqual(0).WithMessage("No point size");
      RuleFor(o => o.StepValue).NotNull().NotEqual(0).WithMessage("No point value");
      RuleFor(o => o.TimeFrame).NotNull().WithMessage("No time frame");
      RuleFor(o => o.Points).NotNull().WithMessage("No points");
      RuleFor(o => o.PointGroups).NotNull().WithMessage("No point groups");
    }
  }
}
