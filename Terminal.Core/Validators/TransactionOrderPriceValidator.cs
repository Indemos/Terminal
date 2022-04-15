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
    protected static readonly List<OrderCategoryEnum?> _immediateTypes = new List<OrderCategoryEnum?>
    {
      OrderCategoryEnum.Market
    };

    public TransactionOrderPriceValidator()
    {
      Include(new TransactionOrderValidator());

      When(o => _immediateTypes.Contains(o.Category) is false, () => RuleFor(o => o.Price).NotNull().NotEqual(0).WithMessage("No open price"));
      When(o => Equals(o.Category, OrderSideEnum.Buy) && Equals(o.Category, OrderCategoryEnum.Stop), () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Instrument.PointGroups.Last().Ask).WithMessage("Buy stop is below the offer"));
      When(o => Equals(o.Category, OrderSideEnum.Sell) && Equals(o.Category, OrderCategoryEnum.Stop), () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Instrument.PointGroups.Last().Bid).WithMessage("Sell stop is above the bid"));
      When(o => Equals(o.Category, OrderSideEnum.Buy) && Equals(o.Category, OrderCategoryEnum.Limit), () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Instrument.PointGroups.Last().Ask).WithMessage("Buy limit is above the offer"));
      When(o => Equals(o.Category, OrderSideEnum.Sell) && Equals(o.Category, OrderCategoryEnum.Limit), () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Instrument.PointGroups.Last().Bid).WithMessage("Sell limit is below the bid"));
    }
  }
}
