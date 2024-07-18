using Schwab.Messages;
using System;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Schwab.Mappers
{
  public class InternalMap
  {
    /// <summary>
    /// Get order book
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static DomModel GetDom(AssetMessage message)
    {
      var o = message.Quote;
      var point = new PointModel
      {
        Ask = o.AskPrice,
        Bid = o.BidPrice,
        AskSize = o.AskSize,
        BidSize = o.BidSize,
        Last = o.AskPrice ?? o.BidPrice,
      };

      return new DomModel
      {
        Asks = [point],
        Bids = [point],
      };
    }

    /// <summary>
    /// Get internal option
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static OptionModel GetOption(OptionMessage message)
    {
      var optionInstrument = new InstrumentModel
      {
        Leverage = message.Multiplier ?? 100,
        Name = message.Symbol,
      };

      var instrument = new InstrumentModel
      {
        Leverage = message.Multiplier ?? 1,
        Name = message.Symbol.Split(' ').FirstOrDefault(),
      };

      var bar = new BarModel
      {
        Low = message.LowPrice,
        High = message.LowPrice,
        Open = message.OpenPrice,
        Close = message.ClosePrice
      };

      var point = new PointModel
      {
        Ask = message.Ask,
        AskSize = message.AskSize ?? 0,
        Bid = message.Bid,
        BidSize = message.BidSize ?? 0,
        Last = message.Last,
        Bar = bar,
        Instrument = instrument
      };

      var greeks = new GreekModel
      {
        Rho = message.Rho ?? 0,
        Vega = message.Vega ?? 0,
        Delta = message.Delta ?? 0,
        Gamma = message.Gamma ?? 0,
        Theta = message.Theta ?? 0
      };

      var option = new OptionModel
      {
        OpenInterest = message.OpenInterest ?? 0,
        Strike = message.StrikePrice,
        IntrinsicValue = message.IntrinsicValue ?? 0,
        Volatility = message.Volatility ?? 0,
        Volume = message.TotalVolume ?? 0,
        Point = point,
        Instrument = optionInstrument,
        Greeks = greeks
      };

      switch (message.PutCall.ToUpper())
      {
        case "PUT": option.Side = OptionSideEnum.Put; break;
        case "CALL": option.Side = OptionSideEnum.Call; break;
      }

      if (message.ExpirationDate is not null)
      {
        option.ExpirationDate = message.ExpirationDate;
      }

      return option;
    }

    /// <summary>
    /// Get internal point
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static PointModel GetPoint(AssetMessage message)
    {
      var o = message.Quote;
      var point = new PointModel
      {
        Ask = o.AskPrice,
        Bid = o.BidPrice,
        AskSize = o.AskSize,
        BidSize = o.BidSize,
        Time = DateTimeOffset.FromUnixTimeMilliseconds(o.QuoteTime ?? DateTime.Now.Ticks).UtcDateTime,
        Last = o.AskTime > o.BidTime ? o.AskPrice : o.BidPrice,
        Instrument = new InstrumentModel { Name = message.Symbol }
      };

      return point;
    }

    /// <summary>
    /// Get internal point from bar
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static PointModel GetBar(BarMessage message)
    {
      var point = new PointModel
      {
        Ask = message.Close,
        Bid = message.Close,
        AskSize = message.Volume,
        BidSize = message.Volume,
        Time = DateTimeOffset.FromUnixTimeMilliseconds(message.Datetime ?? DateTime.Now.Ticks).UtcDateTime,
        Last = message.Close
      };

      return point;
    }

    /// <summary>
    /// Convert remote position to local
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static PositionModel GetPosition(PositionMessage message)
    {
      var instrument = new InstrumentModel
      {
        Name = message.Instrument.Symbol
      };

      var action = new TransactionModel
      {
        Instrument = instrument,
        Price = message.AveragePrice,
        Descriptor = message.Instrument.Symbol,
        Volume = message.LongQuantity + message.ShortQuantity,
        CurrentVolume = message.LongQuantity + message.ShortQuantity
      };

      var order = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetPositionSide(message)
      };

      var gainLossPoints = 0.0;
      var gainLoss = message.LongOpenProfitLoss;

      return new PositionModel
      {
        GainLossPointsMax = gainLossPoints,
        GainLossPointsMin = gainLossPoints,
        GainLossPoints = gainLossPoints,
        GainLossMax = gainLoss,
        GainLossMin = gainLoss,
        GainLoss = gainLoss,
        Order = order,
        Orders = [order]
      };
    }

    /// <summary>
    /// Convert remote position side to local
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static OrderSideEnum GetPositionSide(PositionMessage message)
    {
      switch (true)
      {
        case true when message.LongQuantity > 0: return OrderSideEnum.Buy;
        case true when message.ShortQuantity > 0: return OrderSideEnum.Sell;
      }

      return OrderSideEnum.None;
    }

    /// <summary>
    /// Convert remote order to local
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static OrderModel GetOrder(OrderMessage message)
    {
      var subOrders = message
        ?.OrderLegCollection
        ?.ToList();

      var instrument = new InstrumentModel
      {
        Name = string.Join($" / ", subOrders.Select(o => o?.Instrument?.Symbol))
      };

      var action = new TransactionModel
      {
        Id = $"{message.OrderId}",
        Descriptor = message.OrderId,
        Instrument = instrument,
        CurrentVolume = message.FilledQuantity,
        Volume = message.Quantity,
        Time = message.EnteredTime,
        Status = GetStatus(message)
      };

      var inOrder = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetOrderSide(message),
        TimeSpan = GetTimeSpan(message)
      };

      switch (message.OrderType.ToUpper())
      {
        case "STOP":
          inOrder.Type = OrderTypeEnum.Stop;
          inOrder.Price = message.Price;
          break;

        case "LIMIT":
          inOrder.Type = OrderTypeEnum.Limit;
          inOrder.Price = message.Price;
          break;

        case "STOP_LIMIT":
          inOrder.Type = OrderTypeEnum.StopLimit;
          inOrder.Price = message.Price;
          inOrder.ActivationPrice = message.StopPrice;
          break;

        case "NET_DEBIT":
        case "NET_CREDIT":
          inOrder.Type = OrderTypeEnum.Limit;
          inOrder.Price = message.Price;
          break;
      }

      if (subOrders.Count > 1)
      {
        inOrder.Instruction = instrument.Name;

        foreach (var subOrder in subOrders)
        {
          var subInstrument = new InstrumentModel
          {
            Name = subOrder.Instrument.Symbol
          };

          var subAction = new TransactionModel
          {
            Instrument = subInstrument,
            CurrentVolume = subOrder.Quantity,
            Volume = subOrder.Quantity
          };

          inOrder.Orders.Add(new OrderModel
          {
            Transaction = subAction,
            Side = GetSubOrderSide(subOrder.Instruction)
          });
        }
      }

      return inOrder;
    }

    /// <summary>
    /// Convert remote order status to local
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static OrderStatusEnum GetStatus(OrderMessage message)
    {
      switch (message.Status.ToUpper())
      {
        case "FILLED":
        case "REPLACED": return OrderStatusEnum.Filled;
        case "WORKING": return OrderStatusEnum.Partitioned;
        case "REJECTED":
        case "CANCELED":
        case "EXPIRED": return OrderStatusEnum.Canceled;
        case "NEW":
        case "PENDING_RECALL":
        case "PENDING_CANCEL":
        case "PENDING_REPLACE":
        case "PENDING_ACTIVATION":
        case "PENDING_ACKNOWLEDGEMENT":
        case "AWAITING_CONDITION":
        case "AWAITING_PARENT_ORDER":
        case "AWAITING_RELEASE_TIME":
        case "AWAITING_MANUAL_REVIEW":
        case "AWAITING_STOP_CONDITION": return OrderStatusEnum.Pending;
      }

      return OrderStatusEnum.None;
    }

    /// <summary>
    /// Convert remote order side to local
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static OrderSideEnum GetOrderSide(OrderMessage message)
    {
      switch (message.OrderType.ToUpper())
      {
        case "NET_DEBIT": return OrderSideEnum.Buy;
        case "NET_CREDIT": return OrderSideEnum.Sell;
      }

      var position = message
        ?.OrderLegCollection
        ?.FirstOrDefault();

      return GetSubOrderSide(position.Instruction);
    }

    /// <summary>
    /// Convert remote order side to local
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static OrderSideEnum GetSubOrderSide(string status)
    {
      switch (status?.ToUpper())
      {
        case "BUY":
        case "BUY_TO_OPEN":
        case "BUY_TO_CLOSE":
        case "NET_DEBIT": return OrderSideEnum.Buy;

        case "SELL":
        case "SELL_SHORT":
        case "SELL_TO_OPEN":
        case "SELL_TO_CLOSE":
        case "NET_CREDIT": return OrderSideEnum.Sell;
      }

      return OrderSideEnum.None;
    }

    /// <summary>
    /// Convert remote time in force to local
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static OrderTimeSpanEnum GetTimeSpan(OrderMessage message)
    {
      var span = message.Duration;
      var session = message.Session;

      switch (true)
      {
        case true when E(span, "DAY"): return OrderTimeSpanEnum.Day;
        case true when E(span, "FILL_OR_KILL"): return OrderTimeSpanEnum.Fok;
        case true when E(span, "GOOD_TILL_CANCEL"): return OrderTimeSpanEnum.Gtc;
        case true when E(span, "IMMEDIATE_OR_CANCEL"): return OrderTimeSpanEnum.Ioc;
        case true when E(session, "AM"): return OrderTimeSpanEnum.Am;
        case true when E(session, "PM"): return OrderTimeSpanEnum.Pm;
      }

      return OrderTimeSpanEnum.None;
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
