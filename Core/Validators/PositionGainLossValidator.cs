using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PositionGainLossValidator : AbstractValidator<PositionModel?>
  {
    public PositionGainLossValidator()
    {
      RuleFor(o => o.Value).NotEmpty().SetValidator(new PositionGainLossValueValidator());
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class PositionGainLossValueValidator : AbstractValidator<PositionModel>
  {
    public PositionGainLossValueValidator()
    {
      Include(new PositionValueValidator());

      RuleFor(o => o.GainLoss).NotEmpty();
      RuleFor(o => o.GainLossPoints).NotEmpty();
    }
  }
}
