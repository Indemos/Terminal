using IBApi;
using InteractiveBrokers.Messages;
using System.Linq;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace InteractiveBrokers.Mappers
{
  public class ExternalMap
  {
    /// <summary>
    /// Convert remote order from brokerage to local record
    /// </summary>
    /// <param name="orderModel"></param>
    /// <returns></returns>
    public static OpenOrderMessage GetOrder(OrderModel orderModel)
    {
      var action = orderModel.Transaction;
      var order = new Order();
      var contract = new Contract();

      contract.Symbol = action.Instrument.Name;
      contract.Exchange = orderModel.Exchange ?? "SMART";
      contract.SecType = action.Instrument.Security ?? "STK";
      contract.Currency = orderModel.Currency ?? nameof(CurrencyEnum.USD);

      if (string.Equals(action.Instrument.Security, "OPT"))
      {
        var option = action.Option;

        contract.LocalSymbol = option.Option.Instrument.Name;
        contract.Multiplier = $"{option.Option.Instrument.Leverage}";
        contract.Strike = option.Strike.Value;
        contract.LastTradeDateOrContractMonth = $"{option.ExpirationDate:yyyyMMdd}";

        switch (option.Side)
        {
          case OptionSideEnum.Put: contract.Right = "P"; break;
          case OptionSideEnum.Call: contract.Right = "C"; break;
        }
      }

      //{
      //  Quantity = action.Volume,
      //  Symbol = action.Instrument.Name,
      //  TimeInForce = GetTimeSpan(order.TimeSpan.Value),
      //  OrderType = "market"
      //};

      switch (orderModel.Side)
      {
        case OrderSideEnum.Buy: order.Action = "BUY"; break;
        case OrderSideEnum.Sell: order.Action = "SELL"; break;
      }

      var message = new OpenOrderMessage
      {
        Order = order,
        Contract = contract
      };

      //switch (orderModel.Type)
      //{
      //  case OrderTypeEnum.Stop: message.StopPrice = orderModel.Price; break;
      //  case OrderTypeEnum.Limit: message.LimitPrice = orderModel.Price; break;
      //  case OrderTypeEnum.StopLimit: message.StopPrice = orderModel.ActivationPrice; message.LimitPrice = orderModel.Price; break;
      //}

      //if (orderModel.Orders.Any())
      //{
      //  message.OrderClass = "bracket";

      //  switch (orderModel.Side)
      //  {
      //    case OrderSideEnum.Buy:
      //      message.StopLoss = GetBracket(orderModel, 1);
      //      message.TakeProfit = GetBracket(orderModel, -1);
      //      break;

      //    case OrderSideEnum.Sell:
      //      message.StopLoss = GetBracket(orderModel, -1);
      //      message.TakeProfit = GetBracket(orderModel, 1);
      //      break;
      //  }
      //}

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
    //public static Bracket GetBracket(OrderModel order, double direction)
    //{
    //  var nextOrder = order
    //    .Orders
    //    .FirstOrDefault(o => (o.Price - order.Price) * direction > 0);

    //  if (nextOrder is not null)
    //  {
    //    return new OrderBracketMessage { StopPrice = nextOrder.Price };
    //  }

    //  return null;
    //}
  }
}
