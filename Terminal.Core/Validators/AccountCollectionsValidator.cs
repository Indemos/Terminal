using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class AccountCollectionsValidator : AbstractValidator<IAccountModel>
  {
    public AccountCollectionsValidator()
    {
      Include(new AccountValidator());

      RuleFor(o => o.Instruments).NotNull().NotEmpty().WithMessage("No instruments");
      RuleFor(o => o.Orders).NotNull().NotEmpty().WithMessage("No orders");
      RuleFor(o => o.ActiveOrders).NotNull().NotEmpty().WithMessage("No active orders");
      RuleFor(o => o.Positions).NotNull().NotEmpty().WithMessage("No positions");
      RuleFor(o => o.ActivePositions).NotNull().NotEmpty().WithMessage("No active positions");
    }
  }
}
