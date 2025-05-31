using IBApi;
using InteractiveBrokers.Messages;
using System;
using System.Globalization;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace InteractiveBrokers.Mappers
{
  public class InternalMap
  {
    /// <summary>
    /// Get order book
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static PointModel GetPrice(HistoricalTickBidAsk message, InstrumentModel instrument)
    {
      var point = new PointModel
      {
        Ask = message.PriceAsk,
        Bid = message.PriceBid,
        AskSize = (double)message.SizeAsk,
        BidSize = (double)message.SizeBid,
        Last = message.PriceBid,
        Time = DateTimeOffset.FromUnixTimeSeconds(message.Time).UtcDateTime,
        Instrument = instrument
      };

      return point;
    }

    /// <summary>
    /// Convert remote position to local
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static OrderModel GetOrder(OpenOrderMessage message)
    {
      var instrument = GetInstrument(message.Contract);
      var action = new TransactionModel
      {
        Instrument = instrument,
        Id = $"{message.Order.PermId}",
        Volume = (double)Math.Min(message.Order.FilledQuantity, message.Order.TotalQuantity),
        Time = DateTime.TryParse(message.Order.ActiveStartTime, out var o) ? o : DateTime.UtcNow,
        Status = GetOrderStatus(message.OrderState.Status)
      };

      var order = new OrderModel
      {
        Transaction = action,
        Id = $"{message.OrderId}",
        Type = OrderTypeEnum.Market,
        Side = GetOrderSide(message.Order.Action),
        TimeSpan = GetTimeSpan($"{message.Order.Tif}"),
        Volume = (double)message.Order.TotalQuantity
      };

      switch (message.Order.OrderType)
      {
        case "STP":
          order.Type = OrderTypeEnum.Stop;
          order.Price = message.Order.AuxPrice;
          break;

        case "LMT":
          order.Type = OrderTypeEnum.Limit;
          order.Price = message.Order.LmtPrice;
          break;

        case "STP LMT":
          order.Type = OrderTypeEnum.StopLimit;
          order.Price = message.Order.LmtPrice;
          order.ActivationPrice = message.Order.AuxPrice;
          break;
      }

      return order;
    }

    /// <summary>
    /// Convert remote position to local
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static OrderModel GetPosition(PositionMultiMessage message)
    {
      var volume = (double)Math.Abs(message.Position);
      var instrument = GetInstrument(message.Contract);
      var action = new TransactionModel
      {
        Instrument = instrument,
        Descriptor = $"{message.Contract.ConId}",
        Volume = volume
      };

      var order = new OrderModel
      {
        Volume = volume,
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Price = message.AverageCost / (volume * Math.Max(1, instrument.Leverage.Value)),
        Side = message.Position > 0 ? OrderSideEnum.Long : OrderSideEnum.Short
      };

      return order;
    }

    /// <summary>
    /// Convert remote order status to local
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static OrderStatusEnum? GetOrderStatus(string status)
    {
      switch (status)
      {
        case "ApiPending":
        case "Submitted":
        case "PreSubmitted":
        case "PendingSubmit":
        case "PendingCancel": return OrderStatusEnum.Pending;
        case "Inactive":
        case "Cancelled":
        case "ApiCancelled": return OrderStatusEnum.Canceled;
        case "Filled": return OrderStatusEnum.Filled;
      }

      return null;
    }

    /// <summary>
    /// Convert remote order side to local
    /// </summary>
    /// <param name="side"></param>
    /// <returns></returns>
    public static OrderSideEnum? GetOrderSide(string side)
    {
      switch (side)
      {
        case "BUY": return OrderSideEnum.Long;
        case "SELL": return OrderSideEnum.Short;
      }

      return OrderSideEnum.Group;
    }

    /// <summary>
    /// Convert remote time in force to local
    /// </summary>
    /// <param name="span"></param>
    /// <returns></returns>
    public static OrderTimeSpanEnum? GetTimeSpan(string span)
    {
      switch (span)
      {
        case "DAY": return OrderTimeSpanEnum.Day;
        case "GTC": return OrderTimeSpanEnum.Gtc;
      }

      return null;
    }

    /// <summary>
    /// Get external instrument type
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static InstrumentEnum? GetInstrumentType(string message)
    {
      switch (message)
      {
        case "BOND": return InstrumentEnum.Bonds;
        case "STK": return InstrumentEnum.Shares;
        case "IND": return InstrumentEnum.Indices;
        case "OPT": return InstrumentEnum.Options;
        case "FUT": return InstrumentEnum.Futures;
        case "CFD": return InstrumentEnum.Contracts;
        case "CASH": return InstrumentEnum.Currencies;
        case "FOP": return InstrumentEnum.FutureOptions;
      }

      return null;
    }

    /// <summary>
    /// Get instrument from contract
    /// </summary>
    /// <param name="contract"></param>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public static InstrumentModel GetInstrument(Contract contract, InstrumentModel instrument = null)
    {
      var expiration = contract.LastTradeDateOrContractMonth;
      var response = instrument ?? new InstrumentModel();

      response.Id = $"{contract.ConId}";
      response.Name = contract.LocalSymbol;
      response.Exchange = contract.Exchange;
      response.Type = GetInstrumentType(contract.SecType);
      response.Currency = new CurrencyModel { Name = contract.Currency };
      response.Leverage = int.TryParse(contract.Multiplier, out var leverage) ? Math.Max(1, leverage) : 1;

      if (string.IsNullOrEmpty(contract.Symbol) is false)
      {
        response.Basis = new InstrumentModel { Name = contract.Symbol };
      }

      if (string.IsNullOrEmpty(expiration) is false)
      {
        var expirationDate = DateTime.ParseExact(expiration, "yyyyMMdd", CultureInfo.InvariantCulture);
        var derivative = new DerivativeModel
        {
          Strike = contract.Strike,
          TradeDate = expirationDate,
          ExpirationDate = expirationDate
        };

        switch (contract.Right)
        {
          case "P": derivative.Side = OptionSideEnum.Put; break;
          case "C": derivative.Side = OptionSideEnum.Call; break;
        }

        response.Derivative = derivative;
      }

      return response;
    }
  }
}
