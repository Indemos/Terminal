using FluentValidation;
using System.Linq;
using Terminal.Core.Domains;

namespace Terminal.Core.Validators
{
    /// <summary>
    /// Validation rules
    /// </summary>
    public class InstrumentCollectionValidator : AbstractValidator<Instrument>
  {
    public InstrumentCollectionValidator()
    {
      Include(new InstrumentValidator());

      RuleFor(o => o.Points).NotEmpty();
      RuleFor(o => o.Points.LastOrDefault()).SetValidator(new PointValidator());
    }
  }
}
