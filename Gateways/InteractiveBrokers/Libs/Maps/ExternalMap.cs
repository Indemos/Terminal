using IBApi;
using InteractiveBrokers.Messages;
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
      var contract = new Contract
      {
        Symbol = action.Instrument.Name,
        SecType = action.Instrument.Security ?? "STK",
        Exchange = action.Instrument.Exchange ?? "SMART",
        Currency = action.Instrument.Currency?.Name ?? nameof(CurrencyEnum.USD)
      };

      if (string.Equals(contract.SecType, "OPT"))
      {
        var option = action.Derivative;

        contract.Strike = option.Strike.Value;
        contract.LocalSymbol = option.Contract.Instrument.Name;
        contract.Multiplier = $"{option.Contract.Instrument.Leverage}";
        contract.LastTradeDateOrContractMonth = $"{option.Expiration:yyyyMMdd}";

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
