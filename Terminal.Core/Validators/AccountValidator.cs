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
      RuleFor(o => o.Leverage).NotEmpty();
      RuleFor(o => o.Balance).NotEmpty();
      RuleFor(o => o.InitialBalance).NotEmpty();
      RuleFor(o => o.Currency).NotEmpty();
    }
  }
}
