using FluentValidation;
using Terminal.Core.Domains;

namespace Terminal.Core.Validators
{
    /// <summary>
    /// Validation rules
    /// </summary>
    public class AccountValidator : AbstractValidator<IAccount>
  {
    public AccountValidator()
    {
      RuleFor(o => o.Name).NotEmpty();
      RuleFor(o => o.Balance).NotEmpty();
      RuleFor(o => o.InitialBalance).NotEmpty();
      RuleFor(o => o.Leverage).NotEmpty();
      RuleFor(o => o.Currency).NotEmpty();
    }
  }
}
