using Core.Common.Enums;
using Core.Common.Models;
using IBApi;
using InteractiveBrokers.Messages;
using System;
using System.Globalization;

namespace InteractiveBrokers.Mappers
{
  public class Downstream
  {
    /// <summary>
    /// Get order book
    /// </summary>
    /// <param name="message"></param>
    public static PriceModel GetPrice(HistoricalTickBidAsk message, InstrumentModel instrument)
    {
      var point = new PriceModel
      {
        Ask = message.PriceAsk,
        Bid = message.PriceBid,
        AskSize = (double)message.SizeAsk,
        BidSize = (double)message.SizeBid,
        Last = message.PriceBid,
        Time = message.Time,
        Name = instrument.Name
      };

      return point;
    }

    /// <summary>
    /// Convert remote position to local
    /// </summary>
    /// <param name="message"></param>
    public static OrderModel GetOrder(OpenOrderMessage message)
    {
      var instrument = GetInstrument(message.Contract);
      var action = new OperationModel
      {
        Instrument = instrument,
        Id = $"{message.Order.PermId}",
        Amount = (double)Math.Min(message.Order.FilledQuantity, message.Order.TotalQuantity),
        Time = long.TryParse(message.Order.ActiveStartTime, out var actionTime) ? actionTime : null,
        Status = OrderStatusEnum.Order
      };

      var order = new OrderModel
      {
        Operation = action,
        Id = $"{message.OrderId}",
        Type = OrderTypeEnum.Market,
        Side = GetOrderSide(message.Order.Action),
        TimeSpan = GetTimeSpan($"{message.Order.Tif}"),
        Amount = (double)message.Order.TotalQuantity
      };

      switch (message.Order.OrderType)
      {
        case "STP":

          order = order with
          {
            Type = OrderTypeEnum.Stop,
            Price = message.Order.AuxPrice
          };

          break;

        case "LMT":

          order = order with
          {
            Type = OrderTypeEnum.Limit,
            Price = message.Order.LmtPrice
          };

          break;

        case "STP LMT":

          order = order with
          {
            Type = OrderTypeEnum.StopLimit,
            Price = message.Order.LmtPrice,
            ActivationPrice = message.Order.AuxPrice
          };

          break;
      }

      return order;
    }

    /// <summary>
    /// Convert remote position to local
    /// </summary>
    /// <param name="message"></param>
    public static OrderModel GetPosition(PositionMultiMessage message)
    {
      var volume = (double)Math.Abs(message.Position);
      var instrument = GetInstrument(message.Contract);
      var action = new OperationModel
      {
        Instrument = instrument,
        Amount = volume
      };

      var order = new OrderModel
      {
        Amount = volume,
        Operation = action,
        Type = OrderTypeEnum.Market,
        Descriptor = $"{message.Contract.ConId}",
        Price = message.AverageCost / (volume * Math.Max(1, instrument.Leverage.Value)),
        Side = message.Position > 0 ? OrderSideEnum.Long : OrderSideEnum.Short
      };

      return order;
    }

    /// <summary>
    /// Convert remote order side to local
    /// </summary>
    /// <param name="side"></param>
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
    public static InstrumentModel GetInstrument(Contract contract, InstrumentModel instrument = null)
    {
      var expiration = contract.LastTradeDateOrContractMonth;
      var response = (instrument ?? new InstrumentModel()) with
      {
        Id = $"{contract.ConId}",
        Name = contract.LocalSymbol,
        Exchange = contract.Exchange,
        Type = GetInstrumentType(contract.SecType),
        Currency = new CurrencyModel { Name = contract.Currency },
        Leverage = int.TryParse(contract.Multiplier, out var margin) ? Math.Max(1, margin) : 1
      };

      if (string.IsNullOrEmpty(contract.Symbol) is false)
      {
        response = response with { Basis = new InstrumentModel { Name = contract.Symbol } };
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
          case "P": derivative = derivative with { Side = OptionSideEnum.Put }; break;
          case "C": derivative = derivative with { Side = OptionSideEnum.Call }; break;
        }

        response = response with { Derivative = derivative };
      }

      return response;
    }
  }
}
