using Core.Models;
using FluentValidation;

namespace Core.Validators
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
