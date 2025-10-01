using Core.Common.Enums;
using Core.Common.Models;
using FluentValidation;

namespace Core.Common.Validators
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class InstrumentValidator : AbstractValidator<InstrumentModel>
  {
    public InstrumentValidator()
    {
      RuleFor(o => o.Name).NotEmpty();
      RuleFor(o => o.Commission).NotEmpty();
      RuleFor(o => o.ContractSize).NotEmpty();
      RuleFor(o => o.StepSize).NotEmpty();
      RuleFor(o => o.StepValue).NotEmpty();
      RuleFor(o => o.Leverage).NotEmpty();

      When(o => o.Price is not null, () =>
      {
        RuleFor(o => o.Price).SetValidator(new PriceValidator());
      });

      When(o => o.Type is InstrumentEnum.Options or InstrumentEnum.FutureOptions, () =>
      {
        RuleFor(o => o.Derivative.Side).NotEmpty();
        RuleFor(o => o.Derivative.Strike).NotEmpty();
        RuleFor(o => o.Derivative.TradeDate).NotEmpty();
        RuleFor(o => o.Derivative.ExpirationDate).NotEmpty();
      });

      When(o => o.Type is InstrumentEnum.Futures, () =>
      {
        RuleFor(o => o.Derivative.ExpirationDate).NotEmpty();
      });
    }
  }
}
