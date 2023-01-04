using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class AccountCollectionValidator : AbstractValidator<IAccountModel>
  {
    public AccountCollectionValidator()
    {
      Include(new AccountValidator());

      RuleFor(o => o.Instruments).NotEmpty();
      RuleFor(o => o.Orders).NotEmpty();
      RuleFor(o => o.ActiveOrders).NotEmpty();
      RuleFor(o => o.Positions).NotEmpty();
      RuleFor(o => o.ActivePositions).NotEmpty();
    }
  }
}
