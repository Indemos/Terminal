using Core.Common.States;
using FluentValidation;

namespace Core.Common.Validators
{
    /// <summary>
    /// Validation rules
    /// </summary>
    public class AccountValidator : AbstractValidator<AccountState>
  {
    public AccountValidator()
    {
      RuleFor(o => o.Balance).NotEmpty();
      RuleFor(o => o.Descriptor).NotEmpty();
    }
  }
}
