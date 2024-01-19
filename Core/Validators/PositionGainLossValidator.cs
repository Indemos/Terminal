using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PositionGainLossValidator : AbstractValidator<PositionModel>
  {
    public PositionGainLossValidator()
    {
      Include(new PositionValidator());

      RuleFor(o => o.GainLoss).NotEmpty();
      RuleFor(o => o.GainLossPoints).NotEmpty();
    }
  }
}
