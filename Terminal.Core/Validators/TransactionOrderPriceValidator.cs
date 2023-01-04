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

      When(o => _immediateTypes.Contains(o.Type) is false, () => RuleFor(o => o.Price).NotEmpty());
      When(o => Equals(o.Type, OrderSideEnum.Buy) && Equals(o.Type, OrderTypeEnum.Stop), () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Instrument.PointGroups.Last().Ask));
      When(o => Equals(o.Type, OrderSideEnum.Sell) && Equals(o.Type, OrderTypeEnum.Stop), () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Instrument.PointGroups.Last().Bid));
      When(o => Equals(o.Type, OrderSideEnum.Buy) && Equals(o.Type, OrderTypeEnum.Limit), () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Instrument.PointGroups.Last().Ask));
      When(o => Equals(o.Type, OrderSideEnum.Sell) && Equals(o.Type, OrderTypeEnum.Limit), () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Instrument.PointGroups.Last().Bid));
    }
  }
}
