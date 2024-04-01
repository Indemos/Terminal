using FluentValidation;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class OrderValidator : AbstractValidator<OrderModel?>
  {
    public OrderValidator()
    {
      RuleFor(o => o.Value).NotEmpty().SetValidator(new OrderValueValidator());
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class OrderValueValidator : AbstractValidator<OrderModel>
  {
    public OrderValueValidator()
    {
      RuleFor(o => o.Side).NotEmpty();
      RuleFor(o => o.Type).NotEmpty();
      RuleFor(o => o.TimeSpan).NotEmpty();
      RuleFor(o => o.Transaction).NotEmpty().SetValidator(new TransactionValidator());
    }
  }
}
