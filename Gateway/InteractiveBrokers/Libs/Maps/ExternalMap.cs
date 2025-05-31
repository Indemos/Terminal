using IBApi;
using InteractiveBrokers.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace InteractiveBrokers.Mappers
{
  public class ExternalMap
  {
    /// <summary>
    /// Convert remote order from brokerage to local record
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="orderModel"></param>
    /// <param name="account"></param>
    /// <returns></returns>
    public static IList<OpenOrderMessage> GetOrders(int orderId, OrderModel orderModel, IAccount account)
    {
      var response = new List<OpenOrderMessage>();
      var order = new Order();
      var action = orderModel.Transaction;
      var instrument = action.Instrument;
      var contract = GetContract(action.Instrument);

      order.OrderId = orderId;
      order.Action = GetSide(orderModel.Side);
      order.Tif = GetTimeSpan(orderModel.TimeSpan);
      order.OrderType = GetOrderType(orderModel.Type);
      order.TotalQuantity = (decimal)orderModel.Volume;
      order.ExtOperator = orderModel.Descriptor;
      order.Account = account.Descriptor;

      switch (orderModel.Type)
      {
        case OrderTypeEnum.Stop: order.AuxPrice = orderModel.Price.Value; break;
        case OrderTypeEnum.Limit: order.LmtPrice = orderModel.Price.Value; break;
        case OrderTypeEnum.StopLimit:
          order.LmtPrice = orderModel.Price.Value;
          order.AuxPrice = orderModel.ActivationPrice.Value;
          break;
      }

      var TP = GetBracePrice(orderModel, orderModel.Side is OrderSideEnum.Long ? 1 : -1);
      var SL = GetBracePrice(orderModel, orderModel.Side is OrderSideEnum.Long ? -1 : 1);

      response = [.. GetBraces(order, SL, TP).Select(o => new OpenOrderMessage
      {
        Order = o,
        Contract = contract
      })];

      return response;
    }

    /// <summary>
    /// Instrument to contract
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public static Contract GetContract(InstrumentModel instrument)
    {
      var basis = instrument.Basis;
      var derivative = instrument.Derivative;
      var contract = new Contract
      {
        Symbol = instrument.Name,
        Exchange = instrument.Exchange,
        SecType = GetInstrumentType(instrument.Type),
        Currency = instrument.Currency?.Name ?? nameof(CurrencyEnum.USD),
        ConId = int.TryParse(instrument.Id, out var id) ? id : 0
      };

      if (Equals(instrument.Name, basis?.Name) is false)
      {
        contract.Symbol = basis?.Name;
        contract.LocalSymbol = instrument.Name;
      }

      if (derivative is not null)
      {
        contract.Strike = derivative.Strike ?? 0;
        contract.LastTradeDateOrContractMonth = $"{derivative.TradeDate:yyyyMMdd}";

        switch (derivative.Side)
        {
          case OptionSideEnum.Put: contract.Right = "P"; break;
          case OptionSideEnum.Call: contract.Right = "C"; break;
        }
      }

      return contract;
    }

    /// <summary>
    /// Convert local time in force to remote
    /// </summary>
    /// <param name="span"></param>
    /// <returns></returns>
    public static string GetTimeSpan(OrderTimeSpanEnum? span)
    {
      switch (span)
      {
        case OrderTimeSpanEnum.Day: return "DAY";
        case OrderTimeSpanEnum.Gtc: return "GTC";
      }

      return null;
    }

    /// <summary>
    /// Order side
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static string GetSide(OrderSideEnum? side)
    {
      switch (side)
      {
        case OrderSideEnum.Long: return "BUY";
        case OrderSideEnum.Short: return "SELL";
      }

      return null;
    }

    /// <summary>
    /// Order side
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static string GetOrderType(OrderTypeEnum? message)
    {
      switch (message)
      {
        case OrderTypeEnum.Stop: return "STP";
        case OrderTypeEnum.Limit: return "LMT";
        case OrderTypeEnum.StopLimit: return "STP LMT";
      }

      return "MKT";
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
        case InstrumentEnum.Bonds: return "BOND";
        case InstrumentEnum.Shares: return "STK";
        case InstrumentEnum.Indices: return "IND";
        case InstrumentEnum.Options: return "OPT";
        case InstrumentEnum.Futures: return "FUT";
        case InstrumentEnum.Contracts: return "CFD";
        case InstrumentEnum.Currencies: return "CASH";
        case InstrumentEnum.FutureOptions: return "FOP";
      }

      return null;
    }

    /// <summary>
    /// Get external instrument type
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static string GetExpiration(DateTime? date)
    {
      if (date is not null)
      {
        return $"{date?.Year:0000}{date?.Month:00}";
      }

      return null;
    }

    /// <summary>
    /// Bracket template
    /// </summary>
    /// <param name="order"></param>
    /// <param name="stopPrice"></param>
    /// <param name="takePrice"></param>
    /// <returns></returns>
    public static IList<Order> GetBraces(Order order, double? stopPrice, double? takePrice)
    {
      var orders = new List<Order> { order };

      if (takePrice is not null)
      {
        order.Transmit = false;

        var TP = new Order
        {
          OrderType = "LMT",
          OrderId = order.OrderId + 1,
          Action = order.Action.Equals("BUY") ? "SELL" : "BUY",
          TotalQuantity = order.TotalQuantity,
          LmtPrice = takePrice.Value,
          ParentId = order.OrderId,
          Transmit = false
        };

        orders.Add(TP);
      }

      if (stopPrice is not null)
      {
        order.Transmit = false;

        var SL = new Order
        {
          OrderType = "STP",
          OrderId = order.OrderId + 2,
          Action = order.Action.Equals("BUY") ? "SELL" : "BUY",
          TotalQuantity = order.TotalQuantity,
          AuxPrice = stopPrice.Value,
          ParentId = order.OrderId,
          Transmit = false
        };

        orders.Add(SL);
      }

      orders.Last().Transmit = true;

      return orders;
    }

    /// <summary>
    /// Get price for brackets
    /// </summary>
    /// <param name="order"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    public static double? GetBracePrice(OrderModel order, double direction)
    {
      var nextOrder = order
        .Orders
        .Where(o => Equals(o.Name, order.Name))
        .FirstOrDefault(o => (o.Price - order.Price) * direction > 0);

      return nextOrder?.Price;
    }

    /// <summary>
    /// Get field name by code
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public static T? GetEnum<T>(int code) where T : struct, Enum => Enum.IsDefined(typeof(T), code) ? (T)(object)code : null;

    /// <summary>
    /// Get field name by code
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public static T? GetEnum<T>(string code) where T : struct, Enum => Enum.TryParse(code, true, out T o) ? o : null;
  }
}
