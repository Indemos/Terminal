using Alpaca.Messages;
using System;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Alpaca.Mappers
{
  public class InternalMap
  {
    /// <summary>
    /// Get order book
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static DomModel GetDom(QuoteMessage message)
    {
      var point = new PointModel
      {
        Ask = message.AskPrice,
        Bid = message.BidPrice,
        AskSize = message.AskSize,
        BidSize = message.BidSize,
        Last = message.AskPrice ?? message.BidPrice,
      };

      return new DomModel
      {
        Asks = [point],
        Bids = [point],
      };
    }

    /// <summary>
    /// Get option contract
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static InstrumentModel GetOption(OptionSnapshotMessage message)
    {
      var point = new PointModel
      {
        Ask = message.Quote.AskPrice,
        Bid = message.Quote.BidPrice,
        AskSize = message.Quote.AskSize,
        BidSize = message.Quote.BidSize,
        Last = message.Quote.AskPrice ?? message.Quote.BidPrice
      };

      var derivative = new DerivativeModel
      {
        Expiration = message.Quote.TimestampUtc
      };

      var option = new InstrumentModel
      {
        Point = point,
        Derivative = derivative
      };

      return option;
    }

    /// <summary>
    /// Get point
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static PointModel GetPoint(QuoteMessage message)
    {
      var point = new PointModel
      {
        Ask = message.AskPrice,
        Bid = message.BidPrice,
        AskSize = message.AskSize,
        BidSize = message.BidSize,
        Time = message.TimestampUtc ?? DateTime.UtcNow,
        Last = message.BidPrice ?? message.AskPrice,
        Instrument = new InstrumentModel { Name = message.Symbol }
      };

      return point;
    }

    /// <summary>
    /// Get bar
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
        Time = message.TimeUtc ?? DateTime.UtcNow,
        Last = message.Close,
        Instrument = new InstrumentModel { Name = message.Symbol },
        Bar = new BarModel
        {
          Low = message.Low,
          High = message.High,
          Open = message.Open,
          Close = message.Close
        }
      };

      return point;
    }

    /// <summary>
    /// Convert remote order to local
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static OrderModel GetOrder(OrderMessage message)
    {
      var instrument = new InstrumentModel
      {
        Name = message.Symbol
      };

      var action = new TransactionModel
      {
        Id = $"{message.OrderId}",
        Descriptor = message.ClientOrderId,
        Instrument = instrument,
        CurrentVolume = message.FilledQuantity,
        Volume = message.Quantity,
        Time = message.CreatedAtUtc,
        Status = GetStatus(message.OrderStatus)
      };

      var inOrder = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetOrderSide(message.OrderSide),
        TimeSpan = GetTimeSpan(message.TimeInForce)
      };

      switch (message.OrderType)
      {
        case "stop":
          inOrder.Type = OrderTypeEnum.Stop;
          inOrder.Price = message.StopPrice;
          break;

        case "limit":
          inOrder.Type = OrderTypeEnum.Limit;
          inOrder.Price = message.LimitPrice;
          break;

        case "stop_limit":
          inOrder.Type = OrderTypeEnum.StopLimit;
          inOrder.Price = message.StopPrice;
          inOrder.ActivationPrice = message.LimitPrice;
          break;
      }

      return inOrder;
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
        Name = message.Symbol
      };

      var action = new TransactionModel
      {
        Id = $"{message.AssetId}",
        Descriptor = message.Symbol,
        Instrument = instrument,
        Price = message.AverageEntryPrice,
        CurrentVolume = message.AvailableQuantity,
        Volume = message.Quantity
      };

      var order = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetPositionSide(message.Side)
      };

      var gainLossPoints = message.AverageEntryPrice - message.AssetCurrentPrice;
      var gainLoss = message.CostBasis - message.MarketValue;

      return new PositionModel
      {
        GainLossPointsMax = gainLossPoints,
        GainLossPointsMin = gainLossPoints,
        GainLossPoints = gainLossPoints,
        GainLossMax = gainLoss,
        GainLossMin = gainLoss,
        GainLoss = gainLoss,
        Order = order
      };
    }

    /// <summary>
    /// Convert remote order status to local
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static OrderStatusEnum GetStatus(string status)
    {
      switch (status)
      {
        case "fill":
        case "filled": return OrderStatusEnum.Filled;
        case "partial_fill":
        case "partially_filled": return OrderStatusEnum.Partitioned;
        case "stopped":
        case "expired":
        case "rejected":
        case "canceled":
        case "done_for_day": return OrderStatusEnum.Canceled;
        case "new":
        case "held":
        case "accepted":
        case "suspended":
        case "pending_new":
        case "pending_cancel":
        case "pending_replace": return OrderStatusEnum.Pending;
      }

      return OrderStatusEnum.None;
    }

    /// <summary>
    /// Convert remote order side to local
    /// </summary>
    /// <param name="side"></param>
    /// <returns></returns>
    public static OrderSideEnum GetOrderSide(string side)
    {
      switch (side)
      {
        case "buy": return OrderSideEnum.Buy;
        case "sell": return OrderSideEnum.Sell;
      }

      return OrderSideEnum.None;
    }

    /// <summary>
    /// Convert remote position side to local
    /// </summary>
    /// <param name="side"></param>
    /// <returns></returns>
    public static OrderSideEnum GetPositionSide(string side)
    {
      switch (side)
      {
        case "long": return OrderSideEnum.Buy;
        case "short": return OrderSideEnum.Sell;
      }

      return OrderSideEnum.None;
    }

    /// <summary>
    /// Convert remote time in force to local
    /// </summary>
    /// <param name="span"></param>
    /// <returns></returns>
    public static OrderTimeSpanEnum GetTimeSpan(string span)
    {
      switch (span)
      {
        case "day": return OrderTimeSpanEnum.Day;
        case "fok": return OrderTimeSpanEnum.Fok;
        case "gtc": return OrderTimeSpanEnum.Gtc;
        case "ioc": return OrderTimeSpanEnum.Ioc;
        case "opg": return OrderTimeSpanEnum.Am;
        case "cls": return OrderTimeSpanEnum.Pm;
      }

      return OrderTimeSpanEnum.None;
    }
  }
}
