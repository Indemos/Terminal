using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class AccountValidator : AbstractValidator<IAccountModel>
  {
    public AccountValidator()
    {
      RuleFor(o => o.Leverage).NotNull().NotEqual(0).WithMessage("No leverage");
      RuleFor(o => o.Balance).NotNull().WithMessage("No balance");
      RuleFor(o => o.InitialBalance).NotNull().WithMessage("No initial balance");
      RuleFor(o => o.Currency).NotNull().WithMessage("No currency");
      RuleFor(o => o.Instruments).NotNull().WithMessage("No instruments");
      RuleFor(o => o.Orders).NotNull().WithMessage("No orders");
      RuleFor(o => o.ActiveOrders).NotNull().WithMessage("No active orders");
      RuleFor(o => o.Positions).NotNull().WithMessage("No positions");
      RuleFor(o => o.ActivePositions).NotNull().WithMessage("No active positions");
    }
  }
}
