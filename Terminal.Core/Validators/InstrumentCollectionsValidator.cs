using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class InstrumentCollectionsValidator : AbstractValidator<IInstrumentModel>
  {
    public InstrumentCollectionsValidator()
    {
      Include(new InstrumentValidator());

      RuleFor(o => o.Points).NotNull().NotEmpty().WithMessage("No points");
      RuleFor(o => o.PointGroups).NotNull().NotEmpty().WithMessage("No point groups");
    }
  }
}
