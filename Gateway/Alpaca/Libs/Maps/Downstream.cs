using Alpaca.Markets;
using System;
using System.Text.RegularExpressions;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Alpaca.Mappers
{
  public class Downstream
  {
    /// <summary>
    /// Get order book
    /// </summary>
    /// <param name="message"></param>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public static PointModel GetPrice(IQuote message, InstrumentModel instrument)
    {
      var point = new PointModel
      {
        Ask = (double)message.AskPrice,
        Bid = (double)message.BidPrice,
        AskSize = (double)message.AskSize,
        BidSize = (double)message.BidSize,
        Last = (double)message.BidPrice,
        Time = message.TimestampUtc,
        Name = instrument.Name
      };

      return point;
    }

    /// <summary>
    /// Get option contract
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="message"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static InstrumentModel GetOption(OptionChainRequest screener, IOptionSnapshot message, string name)
    {
      var o = message.Quote;
      var point = new PointModel
      {
        Ask = (double)o.AskPrice,
        Bid = (double)o.BidPrice,
        Last = (double)o.BidPrice,
        AskSize = (double)o.AskSize,
        BidSize = (double)o.BidSize
      };

      var basis = new InstrumentModel
      {
        Name = screener.UnderlyingSymbol
      };

      var option = new InstrumentModel
      {
        Point = point,
        Derivative = GetDerivative(name),
        Name = name
      };

      return option;
    }

    /// <summary>
    /// Convert remote order to local
    /// </summary>
    /// <param name="message"></param>
    /// <param name="account"></param>
    /// <returns></returns>
    public static OrderModel GetOrder(IOrder message, Terminal.Core.Domains.IAccount account)
    {
      var instrument = new InstrumentModel
      {
        Name = message.Symbol,
        Type = GetInstrumentType(message.AssetClass)
      };

      var order = new OrderModel
      {
        Account = account,
        Type = OrderTypeEnum.Market,
        Time = message.CreatedAtUtc,
        Side = GetOrderSide(message.OrderSide),
        Status = GetStatus(message.OrderStatus),
        TimeSpan = GetTimeSpan(message.TimeInForce),
        Amount = GetValue(message.Quantity, message.FilledQuantity),
        OpenAmount = GetValue(message.FilledQuantity, message.Quantity),
        Id = $"{message.OrderId}",
        Name = message.Symbol
      };

      switch (message.OrderType)
      {
        case OrderType.Stop:
          order.Type = OrderTypeEnum.Stop;
          order.OpenPrice = (double)message.StopPrice;
          break;

        case OrderType.Limit:
          order.Type = OrderTypeEnum.Limit;
          order.OpenPrice = (double)message.LimitPrice;
          break;

        case OrderType.StopLimit:
          order.Type = OrderTypeEnum.StopLimit;
          order.OpenPrice = (double)message.StopPrice;
          order.ActivationPrice = (double)message.LimitPrice;
          break;
      }

      account.States.Get(instrument.Name).Instrument = instrument;

      return order;
    }

    /// <summary>
    /// Convert remote position to local
    /// </summary>
    /// <param name="message"></param>
    /// <param name="account"></param>
    /// <returns></returns>
    public static OrderModel GetPosition(IPosition message, Terminal.Core.Domains.IAccount account)
    {
      var price = (double)message.AssetCurrentPrice;
      var point = new PointModel
      {
        Bid = price,
        Ask = price,
        Last = price
      };

      var instrument = new InstrumentModel
      {
        Point = point,
        Name = message.Symbol,
        Type = GetInstrumentType(message.AssetClass)
      };

      if (instrument.Type is InstrumentEnum.Options)
      {
        instrument.Leverage = 100;
      }

      var order = new OrderModel
      {
        Price = price,
        Account = account,
        Name = message.Symbol,
        Type = OrderTypeEnum.Market,
        Side = GetPositionSide(message.Side),
        OpenPrice = (double)message.AverageEntryPrice,
        Amount = GetValue(message.Quantity, message.AvailableQuantity),
        OpenAmount = GetValue(message.Quantity, message.AvailableQuantity),
        Descriptor = $"{message.AssetId}"
      };

      account.States.Get(instrument.Name).Instrument = instrument;

      return order;
    }

    /// <summary>
    /// Convert remote order status to local
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static OrderStatusEnum? GetStatus(OrderStatus status)
    {
      switch (status)
      {
        case OrderStatus.Fill:
        case OrderStatus.Filled: return OrderStatusEnum.Filled;
        case OrderStatus.PartialFill:
        case OrderStatus.PartiallyFilled: return OrderStatusEnum.Partitioned;
        case OrderStatus.Stopped:
        case OrderStatus.Expired:
        case OrderStatus.Rejected:
        case OrderStatus.Canceled:
        case OrderStatus.DoneForDay: return OrderStatusEnum.Canceled;
        case OrderStatus.New:
        case OrderStatus.Held:
        case OrderStatus.Accepted:
        case OrderStatus.Suspended:
        case OrderStatus.PendingNew:
        case OrderStatus.PendingCancel:
        case OrderStatus.PendingReplace: return OrderStatusEnum.Pending;
      }

      return null;
    }

    /// <summary>
    /// Convert remote order side to local
    /// </summary>
    /// <param name="side"></param>
    /// <returns></returns>
    public static OrderSideEnum? GetOrderSide(OrderSide? side)
    {
      switch (side)
      {
        case OrderSide.Buy: return OrderSideEnum.Long;
        case OrderSide.Sell: return OrderSideEnum.Short;
      }

      return OrderSideEnum.Group;
    }

    /// <summary>
    /// Convert remote order side to local
    /// </summary>
    /// <param name="side"></param>
    /// <returns></returns>
    public static OrderSideEnum? GetTakerSide(TakerSide side)
    {
      switch (side)
      {
        case TakerSide.Buy: return OrderSideEnum.Long;
        case TakerSide.Sell: return OrderSideEnum.Short;
      }

      return null;
    }

    /// <summary>
    /// Convert remote position side to local
    /// </summary>
    /// <param name="side"></param>
    /// <returns></returns>
    public static OrderSideEnum? GetPositionSide(PositionSide side)
    {
      switch (side)
      {
        case PositionSide.Long: return OrderSideEnum.Long;
        case PositionSide.Short: return OrderSideEnum.Short;
      }

      return null;
    }

    /// <summary>
    /// Convert remote time in force to local
    /// </summary>
    /// <param name="span"></param>
    /// <returns></returns>
    public static OrderTimeSpanEnum? GetTimeSpan(TimeInForce span)
    {
      switch (span)
      {
        case TimeInForce.Day: return OrderTimeSpanEnum.Day;
        case TimeInForce.Fok: return OrderTimeSpanEnum.Fok;
        case TimeInForce.Gtc: return OrderTimeSpanEnum.Gtc;
        case TimeInForce.Ioc: return OrderTimeSpanEnum.Ioc;
        case TimeInForce.Opg: return OrderTimeSpanEnum.Am;
        case TimeInForce.Cls: return OrderTimeSpanEnum.Pm;
      }

      return null;
    }

    /// <summary>
    /// Asset type
    /// </summary>
    /// <param name="assetType"></param>
    /// <returns></returns>
    public static InstrumentEnum? GetInstrumentType(AssetClass? assetType)
    {
      switch (assetType)
      {
        case AssetClass.Crypto: return InstrumentEnum.Coins;
        case AssetClass.UsEquity: return InstrumentEnum.Shares;
        case AssetClass.UsOption: return InstrumentEnum.Options;
      }

      return null;
    }

    /// <summary>
    /// Get derivative model based on option name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static DerivativeModel GetDerivative(string name)
    {
      var data = Regex.Match(name, @"^(\w{1,5})(\d{6})([CP])(\d{8})$");

      if (data.Success is false)
      {
        return null;
      }

      var strike = double.Parse(data.Groups[4].Value) / 1000.0;
      var expiration = DateTime.ParseExact(data.Groups[2].Value, "yyMMdd", null);
      var derivative = new DerivativeModel
      {
        Strike = strike,
        ExpirationDate = expiration,
        TradeDate = expiration
      };

      switch (data.Groups[3].Value.ToUpper())
      {
        case "P": derivative.Side = OptionSideEnum.Put; break;
        case "C": derivative.Side = OptionSideEnum.Call; break;
      }

      return derivative;
    }

    /// <summary>
    /// Get value or default
    /// </summary>
    /// <param name="price"></param>
    /// <param name="origin"></param>
    /// <returns></returns>
    public static double GetValue(decimal? price, decimal? origin) => (double)(price is 0 or null ? origin : price);
  }
}
