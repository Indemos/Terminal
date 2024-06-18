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
    /// Get internal option
    /// </summary>
    /// <param name="optionMessage"></param>
    /// <returns></returns>
    public static OptionModel GetOption(OptionSnapshotMessage optionMessage)
    {
      var point = new PointModel
      {
        Ask = optionMessage.Quote.AskPrice,
        Bid = optionMessage.Quote.BidPrice,
        AskSize = optionMessage.Quote.AskSize,
        BidSize = optionMessage.Quote.BidSize,
        Last = optionMessage.Quote.AskPrice ?? optionMessage.Quote.BidPrice
      };

      var option = new OptionModel
      {
        Point = point,
        ExpirationDate = optionMessage.Quote.TimestampUtc        
      };

      return option;
    }

    /// <summary>
    /// Get internal point
    /// </summary>
    /// <param name="pointMessage"></param>
    /// <returns></returns>
    public static PointModel GetPoint(HistoricalQuoteMessage pointMessage)
    {
      var point = new PointModel
      {
        Ask = pointMessage.AskPrice,
        Bid = pointMessage.BidPrice,
        AskSize = pointMessage.AskSize,
        BidSize = pointMessage.BidSize,
        Time = pointMessage.TimestampUtc ?? DateTime.UtcNow,
        Last = pointMessage.BidPrice ?? pointMessage.AskPrice,
        Instrument = new Instrument { Name = pointMessage.Symbol }
      };

      return point;
    }

    /// <summary>
    /// Get internal bar
    /// </summary>
    /// <param name="pointMessage"></param>
    /// <returns></returns>
    public static PointModel GetBar(HistoricalBarMessage pointMessage)
    {
      var point = new PointModel
      {
        Ask = pointMessage.Close,
        Bid = pointMessage.Close,
        AskSize = pointMessage.Volume,
        BidSize = pointMessage.Volume,
        Time = pointMessage.TimeUtc ?? DateTime.UtcNow,
        Last = pointMessage.Close,
        Instrument = new Instrument { Name = pointMessage.Symbol },
        Bar = new BarModel
        {
          Low = pointMessage.Low,
          High = pointMessage.High,
          Open = pointMessage.Open,
          Close = pointMessage.Close
        }
      };

      return point;
    }

    /// <summary>
    /// Convert remote order to local
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static OrderModel GetOrder(OrderMessage order)
    {
      var instrument = new Instrument
      {
        Name = order.Symbol
      };

      var action = new TransactionModel
      {
        Id = $"{order.OrderId}",
        Descriptor = order.ClientOrderId,
        Instrument = instrument,
        CurrentVolume = order.FilledQuantity,
        Volume = order.Quantity,
        Time = order.CreatedAtUtc,
        Status = GetStatus(order.OrderStatus)
      };

      var inOrder = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetOrderSide(order.OrderSide),
        TimeSpan = GetTimeSpan(order.TimeInForce)
      };

      switch (order.OrderType)
      {
        case "stop":
          inOrder.Type = OrderTypeEnum.Stop;
          inOrder.Price = order.StopPrice;
          break;

        case "limit":
          inOrder.Type = OrderTypeEnum.Limit;
          inOrder.Price = order.LimitPrice;
          break;

        case "stop_limit":
          inOrder.Type = OrderTypeEnum.StopLimit;
          inOrder.Price = order.StopPrice;
          inOrder.ActivationPrice = order.LimitPrice;
          break;
      }

      return inOrder;
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
        Name = position.Symbol
      };

      var action = new TransactionModel
      {
        Id = $"{position.AssetId}",
        Descriptor = position.Symbol,
        Instrument = instrument,
        Price = position.AverageEntryPrice,
        CurrentVolume = position.AvailableQuantity,
        Volume = position.Quantity
      };

      var order = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetPositionSide(position.Side)
      };

      var gainLossPoints = position.AverageEntryPrice - position.AssetCurrentPrice;
      var gainLoss = position.CostBasis - position.MarketValue;

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
