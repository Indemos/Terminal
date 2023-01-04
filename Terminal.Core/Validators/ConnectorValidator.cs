using FluentValidation;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class ConnectorValidator : AbstractValidator<IConnectorModel>
  {
    public ConnectorValidator()
    {
      RuleFor(o => o.Name).NotEmpty();
    }
  }
}
