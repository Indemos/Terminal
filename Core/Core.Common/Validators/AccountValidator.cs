using Core.Common.Models;
using FluentValidation;

namespace Core.Common.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class AccountValidator : AbstractValidator<AccountModel>
  {
    public AccountValidator()
    {
      RuleFor(o => o.Balance).NotEmpty();
      RuleFor(o => o.Name).NotEmpty();
    }
  }
}
