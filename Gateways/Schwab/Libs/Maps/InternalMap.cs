using Schwab.Messages;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Schwab.Mappers
{
  public class InternalMap
  {
    /// <summary>
    /// Get internal option
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static OptionModel GetOption(OptionMessage message)
    {
      var option = new OptionModel
      {
        Name = message.Symbol,
        OpenInterest = message.OpenInterest ?? 0,
        Strike = message.StrikePrice ?? 0,
        IntrinsicValue = message.IntrinsicValue ?? 0,
        Leverage = message.Multiplier ?? 0,
        Volatility = message.Volatility ?? 0,
        Volume = message.TotalVolume ?? 0,
        Point = new PointModel
        {
          Ask = message.Ask ?? 0,
          AskSize = message.AskSize ?? 0,
          Bid = message.Bid ?? 0,
          BidSize = message.BidSize ?? 0,
          Bar = new BarModel
          {
            Low = message.LowPrice ?? 0,
            High = message.LowPrice ?? 0,
            Open = message.OpenPrice ?? 0,
            Close = message.ClosePrice ?? 0
          }
        },
        Derivatives = new DerivativeModel
        {
          Rho = message.Rho ?? 0,
          Vega = message.Vega ?? 0,
          Delta = message.Delta ?? 0,
          Gamma = message.Gamma ?? 0,
          Theta = message.Theta ?? 0
        }
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
    /// <param name="pointMessage"></param>
    /// <returns></returns>
    public static PointModel GetPoint(AssetMessage pointMessage)
    {
      var o = pointMessage.Quote;
      var point = new PointModel
      {
        Ask = o.AskPrice,
        Bid = o.BidPrice,
        AskSize = o.AskSize,
        BidSize = o.BidSize,
        Time = DateTimeOffset.FromUnixTimeMilliseconds(o.QuoteTime ?? DateTime.Now.Ticks).UtcDateTime,
        Last = o.AskTime > o.BidTime ? o.AskPrice : o.BidPrice,
        Instrument = new Instrument { Name = pointMessage.Symbol }
      };

      return point;
    }

    /// <summary>
    /// Convert remote position to local
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public static PositionModel GetPosition(PositionMessage position)
    {
      var instrument = new Instrument
      {
        Name = position.Instrument.Symbol
      };

      var action = new TransactionModel
      {
        Instrument = instrument,
        Price = position.AveragePrice,
        Descriptor = position.Instrument.Symbol,
        Volume = position.LongQuantity + position.ShortQuantity,
        CurrentVolume = position.LongQuantity + position.ShortQuantity
      };

      var order = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetPositionSide(position)
      };

      var gainLossPoints = 0.0;
      var gainLoss = position.LongOpenProfitLoss;

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
    /// <param name="position"></param>
    /// <returns></returns>
    public static OrderSideEnum GetPositionSide(PositionMessage position)
    {
      switch (true)
      {
        case true when position.LongQuantity > 0: return OrderSideEnum.Buy;
        case true when position.ShortQuantity > 0: return OrderSideEnum.Sell;
      }

      return OrderSideEnum.None;
    }

    /// <summary>
    /// Convert remote order to local
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static OrderModel GetOrder(OrderMessage order)
    {
      var assets = order
        ?.OrderLegCollection
        ?.Select(o => o?.Instrument?.Symbol);

      var instrument = new Instrument
      {
        Name = string.Join($" {Environment.NewLine}", assets)
      };

      var action = new TransactionModel
      {
        Id = $"{order.OrderId}",
        Descriptor = order.OrderId,
        Instrument = instrument,
        CurrentVolume = order.FilledQuantity,
        Volume = order.Quantity,
        Time = order.EnteredTime,
        Status = GetStatus(order)
      };

      var inOrder = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetOrderSide(order),
        TimeSpan = GetTimeSpan(order)
      };

      switch (order.OrderType.ToUpper())
      {
        case "STOP":
          inOrder.Type = OrderTypeEnum.Stop;
          inOrder.Price = order.Price;
          break;

        case "LIMIT":
          inOrder.Type = OrderTypeEnum.Limit;
          inOrder.Price = order.Price;
          break;

        case "STOP_LIMIT":
          inOrder.Type = OrderTypeEnum.StopLimit;
          inOrder.Price = order.Price;
          inOrder.ActivationPrice = order.StopPrice;
          break;
      }

      return inOrder;
    }

    /// <summary>
    /// Convert remote order status to local
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static OrderStatusEnum GetStatus(OrderMessage order)
    {
      switch (order.Status.ToUpper())
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
    /// <param name="order"></param>
    /// <returns></returns>
    public static OrderSideEnum GetOrderSide(OrderMessage order)
    {
      var position = order
        ?.OrderLegCollection
        ?.FirstOrDefault();

      if (E(position.OrderLegType, "EQUITY"))
      {
        switch (position.Instruction.ToUpper())
        {
          case "BUY":
          case "BUY_TO_OPEN":
          case "BUY_TO_CLOSE": return OrderSideEnum.Buy;

          case "SELL":
          case "SELL_TO_OPEN":
          case "SELL_TO_CLOSE": return OrderSideEnum.Sell;
        }
      }

      return OrderSideEnum.None;
    }

    /// <summary>
    /// Convert remote time in force to local
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static OrderTimeSpanEnum GetTimeSpan(OrderMessage order)
    {
      var span = order.Duration;
      var session = order.Session;

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
