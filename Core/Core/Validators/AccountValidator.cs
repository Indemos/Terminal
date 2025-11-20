using Core.Models;
using FluentValidation;

namespace Core.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class AccountValidator : AbstractValidator<Account>
  {
    public AccountValidator()
    {
      RuleFor(o => o.Balance).NotEmpty();
      RuleFor(o => o.Descriptor).NotEmpty();
    }
  }
}
