using FluentValidation;
using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation rules for limit orders
  /// </summary>
  public class OrderPriceValidator : AbstractValidator<OrderModel>
  {
    protected static readonly Dictionary<OrderTypeEnum?, bool> _orderTypes = new()
    {
      [OrderTypeEnum.None] = true,
      [OrderTypeEnum.Market] = true
    };

    public OrderPriceValidator()
    {
      Include(new OrderValidator());

      When(o => _orderTypes.ContainsKey(o.Type ?? OrderTypeEnum.None) is false, () => RuleFor(o => o.Transaction.Price).NotEmpty());
      When(o => Equals(o.Side, OrderSideEnum.Buy) && Equals(o.Type, OrderTypeEnum.Stop), () => RuleFor(o => o.Transaction.Price).GreaterThanOrEqualTo(o => o.Transaction.Instrument.Points.Last().Ask));
      When(o => Equals(o.Side, OrderSideEnum.Sell) && Equals(o.Type, OrderTypeEnum.Stop), () => RuleFor(o => o.Transaction.Price).LessThanOrEqualTo(o => o.Transaction.Instrument.Points.Last().Bid));
      When(o => Equals(o.Side, OrderSideEnum.Buy) && Equals(o.Type, OrderTypeEnum.Limit), () => RuleFor(o => o.Transaction.Price).LessThanOrEqualTo(o => o.Transaction.Instrument.Points.Last().Ask));
      When(o => Equals(o.Side, OrderSideEnum.Sell) && Equals(o.Type, OrderTypeEnum.Limit), () => RuleFor(o => o.Transaction.Price).GreaterThanOrEqualTo(o => o.Transaction.Instrument.Points.Last().Bid));

      When(o => Equals(o.Side, OrderSideEnum.Buy) && Equals(o.Type, OrderTypeEnum.StopLimit), () =>
      {
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).GreaterThanOrEqualTo(o => o.Transaction.Instrument.Points.Last().Ask);
        RuleFor(o => o.Transaction.Price).GreaterThanOrEqualTo(o => o.ActivationPrice);
      });

      When(o => Equals(o.Side, OrderSideEnum.Sell) && Equals(o.Type, OrderTypeEnum.StopLimit), () =>
      {
        RuleFor(o => o.ActivationPrice).NotEmpty();
        RuleFor(o => o.ActivationPrice).LessThanOrEqualTo(o => o.Transaction.Instrument.Points.Last().Bid);
        RuleFor(o => o.Transaction.Price).LessThanOrEqualTo(o => o.ActivationPrice);
      });
    }
  }
}
