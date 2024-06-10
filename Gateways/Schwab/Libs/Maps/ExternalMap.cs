using Schwab.Messages;
using System;
using System.Linq;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Schwab.Mappers
{
  public class ExternalMap
  {
    /// <summary>
    /// Convert remote order from brokerage to local record
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static OrderMessage GetOrder(OrderModel order)
    {
      var action = order.Transaction;
      var message = new OrderMessage
      {
        Duration = "DAY",
        Session = "NORMAL",
        OrderType = "MARKET",
        OrderStrategyType = order.Combination
      };

      switch (order.Type)
      {
        case OrderTypeEnum.Stop:
          message.OrderType = "STOP";
          message.StopPrice = order.Price;
          break;

        case OrderTypeEnum.Limit:
          message.OrderType = "LIMIT";
          message.Price = order.Price;
          break;

        case OrderTypeEnum.StopLimit:
          message.OrderType = "STOP_LIMIT";
          message.Price = order.Price;
          message.StopPrice = order.Price;
          message.ActivationPrice = order.ActivationPrice;
          break;
      }

      if (E(order.Combination, "TRIGGER"))
      {
        message.OrderLegCollection = [GetOrderItem(order)];
        message.ChildOrderStrategies = order
          .Orders
          .Select(o =>
          {
            var subOrder = GetOrder(o);
            subOrder.OrderLegCollection = [GetOrderItem(o)];
            return subOrder;
          })
          .ToList();
      }
      else
      {
        message.OrderLegCollection = order.Orders.Select(GetOrderItem).ToList();
      }

      return message;
    }

    /// <summary>
    /// Create leg in a combo-order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static OrderLegMessage GetOrderItem(OrderModel order)
    {
      var instrument = new InstrumentMessage
      {
        AssetType = order.AssetType,
        Symbol = order.Transaction.Instrument.Name
      };

      var response = new OrderLegMessage
      {
        Instrument = instrument,
        Quantity = order.Transaction.Volume,
      };

      switch (order.Side)
      {
        case OrderSideEnum.Buy: response.Instruction = "BUY"; break;
        case OrderSideEnum.Sell: response.Instruction = "SELL"; break;
      }

      if (E(order.AssetType, "OPTION"))
      {
        switch (order.Side)
        {
          case OrderSideEnum.Buy: response.Instruction = "BUY_TO_OPEN"; break;
          case OrderSideEnum.Sell: response.Instruction = "SELL_TO_CLOSE"; break;
        }
      }

      return response;
    }

    /// <summary>
    /// Case insensitive comparison
    /// </summary>
    /// <param name="x"></param>
    /// <param name="o"></param>
    /// <returns></returns>
    public static bool E(string x, string o) => string.Equals(x, o, StringComparison.OrdinalIgnoreCase);
  }
}
