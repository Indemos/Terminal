using FluentValidation;
using System.Collections.Generic;
using System.Linq;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules for limit orders
  /// </summary>
  public class TransactionOrderPriceValidator : AbstractValidator<ITransactionOrderModel>
  {
    protected static readonly List<OrderTypeEnum?> _immediateTypes = new()
    {
      OrderTypeEnum.Market
    };

    public TransactionOrderPriceValidator()
    {
      Include(new TransactionOrderValidator());

      When(o => _immediateTypes.Contains(o.Type) is false, () => RuleFor(o => o.Price).NotNull().NotEqual(0).WithMessage("No open price"));
      When(o => Equals(o.Type, OrderSideEnum.Buy) && Equals(o.Type, OrderTypeEnum.Stop), () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Instrument.PointGroups.Last().Ask).WithMessage("Buy stop is below the offer"));
      When(o => Equals(o.Type, OrderSideEnum.Sell) && Equals(o.Type, OrderTypeEnum.Stop), () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Instrument.PointGroups.Last().Bid).WithMessage("Sell stop is above the bid"));
      When(o => Equals(o.Type, OrderSideEnum.Buy) && Equals(o.Type, OrderTypeEnum.Limit), () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Instrument.PointGroups.Last().Ask).WithMessage("Buy limit is above the offer"));
      When(o => Equals(o.Type, OrderSideEnum.Sell) && Equals(o.Type, OrderTypeEnum.Limit), () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Instrument.PointGroups.Last().Bid).WithMessage("Sell limit is below the bid"));
    }
  }
}
