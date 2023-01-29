using FluentValidation;
using System.Linq;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class InstrumentCollectionValidator : AbstractValidator<IInstrumentModel>
  {
    public InstrumentCollectionValidator()
    {
      Include(new InstrumentValidator());

      RuleFor(o => o.Points).NotEmpty();
      RuleFor(o => o.Points.LastOrDefault()).SetValidator(new PointValidator());
    }
  }
}
