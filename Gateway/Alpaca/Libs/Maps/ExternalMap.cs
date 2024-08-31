using Alpaca.Messages;
using System.Linq;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Alpaca.Mappers
{
  public class ExternalMap
  {
    /// <summary>
    /// Convert remote order from brokerage to local record
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static OrderCreationMessage GetOrder(OrderModel order)
    {
      var action = order.Transaction;
      var message = new OrderCreationMessage
      {
        Quantity = action.CurrentVolume,
        Symbol = action.Instrument.Name,
        TimeInForce = GetTimeSpan(order.TimeSpan.Value),
        OrderType = "market"
      };

      switch (order.Side)
      {
        case OrderSideEnum.Buy: message.OrderSide = "buy"; break;
        case OrderSideEnum.Sell: message.OrderSide = "sell"; break;
      }

      switch (order.Type)
      {
        case OrderTypeEnum.Stop: message.StopPrice = order.Price; break;
        case OrderTypeEnum.Limit: message.LimitPrice = order.Price; break;
        case OrderTypeEnum.StopLimit: message.StopPrice = order.ActivationPrice; message.LimitPrice = order.Price; break;
      }

      if (order.Orders.Count > 0)
      {
        message.OrderClass = "bracket";

        switch (order.Side)
        {
          case OrderSideEnum.Buy:
            message.StopLoss = GetBracket(order, 1);
            message.TakeProfit = GetBracket(order, -1);
            break;

          case OrderSideEnum.Sell:
            message.StopLoss = GetBracket(order, -1);
            message.TakeProfit = GetBracket(order, 1);
            break;
        }
      }

      return null;
    }

    /// <summary>
    /// Convert local time in force to remote
    /// </summary>
    /// <param name="span"></param>
    /// <returns></returns>
    public static string GetTimeSpan(OrderTimeSpanEnum span)
    {
      switch (span)
      {
        case OrderTimeSpanEnum.Day: return "day";
        case OrderTimeSpanEnum.Fok: return "fok";
        case OrderTimeSpanEnum.Gtc: return "gtc";
        case OrderTimeSpanEnum.Ioc: return "ioc";
        case OrderTimeSpanEnum.Am: return "opg";
        case OrderTimeSpanEnum.Pm: return "cls";
      }

      return null;
    }

    /// <summary>
    /// Convert child orders to brackets
    /// </summary>
    /// <param name="order"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    public static OrderBracketMessage GetBracket(OrderModel order, double direction)
    {
      var nextOrder = order
        .Orders
        .FirstOrDefault(o => (o.Price - order.Price) * direction > 0);

      if (nextOrder is not null)
      {
        return new OrderBracketMessage { StopPrice = nextOrder.Price };
      }

      return null;
    }
  }
}
