using FluentValidation;
using Terminal.Core.Domains;

namespace Terminal.Core.Validators
{
    /// <summary>
    /// Validation rules
    /// </summary>
    public class AccountCollectionValidator : AbstractValidator<IAccount>
  {
    public AccountCollectionValidator()
    {
      Include(new AccountValidator());

      RuleFor(o => o.Orders).NotEmpty();
      RuleFor(o => o.ActiveOrders).NotEmpty();
      RuleFor(o => o.Positions).NotEmpty();
      RuleFor(o => o.ActivePositions).NotEmpty();
      RuleFor(o => o.Instruments).NotEmpty();
    }
  }
}
