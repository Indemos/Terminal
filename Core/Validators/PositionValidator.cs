using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class PositionValidator : AbstractValidator<PositionModel?>
  {
    public PositionValidator()
    {
      RuleFor(o => o.Value).NotEmpty().SetValidator(new PositionValueValidator());
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class PositionValueValidator : AbstractValidator<PositionModel>
  {
    public PositionValueValidator()
    {
      RuleFor(o => o.Orders).NotEmpty();
      RuleFor(o => o.Order).NotEmpty().SetValidator(new OrderValidator());
    }
  }
}
