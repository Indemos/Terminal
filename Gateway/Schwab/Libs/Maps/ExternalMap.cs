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
        OrderStrategyType = "SINGLE"
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

      message.OrderLegCollection = order
        .Orders
        .Where(o => Equals(o.Instruction, InstructionEnum.Side))
        .Select(GetOrderItem)
        .Concat([GetOrderItem(order)])
        .ToList();

      message.ChildOrderStrategies = order
        .Orders
        .Where(o => Equals(o.Instruction, InstructionEnum.Brace))
        .Select(o =>
        {
          var subOrder = GetOrder(o);
          subOrder.OrderLegCollection = [GetOrderItem(o)];
          return subOrder;
        })
        .ToList();

      if (Equals(order.Instruction, InstructionEnum.Group))
      {
        message.OrderLegCollection.Add(GetOrderItem(order));
      }

      if (message.ChildOrderStrategies.Any())
      {
        message.OrderStrategyType = "TRIGGER";
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
        AssetType = GetInstrumentType(order.Transaction.Instrument.Type),
        Symbol = order.Transaction.Instrument.Name
      };

      var response = new OrderLegMessage
      {
        Instrument = instrument,
        Quantity = order.Transaction.CurrentVolume,
      };

      switch (order.Side)
      {
        case OrderSideEnum.Buy: response.Instruction = "BUY"; break;
        case OrderSideEnum.Sell: response.Instruction = "SELL"; break;
      }

      if (Equals(order.Transaction.Instrument.Type, InstrumentEnum.Options))
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
    /// Get external instrument type
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static string GetInstrumentType(InstrumentEnum? message)
    {
      switch (message)
      {
        case InstrumentEnum.Shares: return "Equity";
        case InstrumentEnum.Options: return "Option";
      }

      return null;
    }
  }
}
