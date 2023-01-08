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
    protected static readonly Dictionary<OrderTypeEnum?, bool> _orderTypes = new()
    {
      [OrderTypeEnum.None] = true,
      [OrderTypeEnum.Market] = true
    };

    public TransactionOrderPriceValidator()
    {
      Include(new TransactionOrderValidator());

      When(o => _orderTypes.ContainsKey(o.Type ?? OrderTypeEnum.None) is false, () => RuleFor(o => o.Price).NotEmpty());
      When(o => Equals(o.Side, OrderSideEnum.Buy) && Equals(o.Type, OrderTypeEnum.Stop), () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Instrument.Points.Last().Ask));
      When(o => Equals(o.Side, OrderSideEnum.Sell) && Equals(o.Type, OrderTypeEnum.Stop), () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Instrument.Points.Last().Bid));
      When(o => Equals(o.Side, OrderSideEnum.Buy) && Equals(o.Type, OrderTypeEnum.Limit), () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Instrument.Points.Last().Ask));
      When(o => Equals(o.Side, OrderSideEnum.Sell) && Equals(o.Type, OrderTypeEnum.Limit), () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Instrument.Points.Last().Bid));

      When(o => Equals(o.Side, OrderSideEnum.Buy) && Equals(o.Type, OrderTypeEnum.StopLimit), () =>
      {
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).GreaterThanOrEqualTo(o => o.Instrument.Points.Last().Ask);
        RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.ActivationPrice);
      });

      When(o => Equals(o.Side, OrderSideEnum.Sell) && Equals(o.Type, OrderTypeEnum.StopLimit), () =>
      {
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).LessThanOrEqualTo(o => o.Instrument.Points.Last().Bid);
        RuleFor(o => o.Price).LessThanOrEqualTo(o => o.ActivationPrice);
      });
    }
  }
}
