using Core.Enums;
using Core.Models;
using IBApi;
using IBApi.Messages;
using System;
using System.Globalization;

namespace InteractiveBrokers.Mappers
{
  public class Downstream
  {
    /// <summary>
    /// Get price
    /// </summary>
    /// <param name="message"></param>
    /// <param name="instrument"></param>
    public static PriceModel Price(PriceMessage message, InstrumentModel instrument)
    {
      var point = new PriceModel
      {
        Ask = message.Ask,
        Bid = message.Bid,
        AskSize = message.AskSize,
        BidSize = message.BidSize,
        Last = message.Last,
        Time = message.Time,
        TimeFrame = instrument.TimeFrame,
        Name = instrument.Name
      };

      return point;
    }

    /// <summary>
    /// Get price
    /// </summary>
    /// <param name="message"></param>
    /// <param name="instrument"></param>
    public static PriceModel Price(HistoricalDataMessage message, InstrumentModel instrument)
    {
      var point = new PriceModel
      {
        Ask = message.Close,
        Bid = message.Close,
        Last = message.Close,
        Time = DateTime.Parse(message.Date).Ticks,
        Name = instrument.Name,
        Volume = (double?)message.Volume,
        Bar = new BarModel
        {
          Low = message.Low,
          High = message.High,
          Open = message.Open,
          Close = message.Close,
        }
      };

      return point;
    }

    /// <summary>
    /// Convert remote position to local
    /// </summary>
    /// <param name="message"></param>
    public static OrderModel Order(OpenOrderMessage message)
    {
      var instrument = Instrument(message.Contract);
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
        Side = OrderSide(message.Order.Action),
        TimeSpan = TimeFrame($"{message.Order.Tif}"),
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
    /// <param name="instrument"></param>
    public static OrderModel Position(PositionMultiMessage message, InstrumentModel instrument)
    {
      var volume = (double)Math.Abs(message.Position);
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
        Price = message.AverageCost,
        Side = message.Position > 0 ? OrderSideEnum.Long : OrderSideEnum.Short
      };

      return order;
    }

    /// <summary>
    /// Convert remote order side to local
    /// </summary>
    /// <param name="side"></param>
    public static OrderSideEnum? OrderSide(string side)
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
    public static OrderTimeSpanEnum? TimeFrame(string span)
    {
      switch (span)
      {
        case "DAY": return OrderTimeSpanEnum.DAY;
        case "GTC": return OrderTimeSpanEnum.GTC;
      }

      return null;
    }

    /// <summary>
    /// Get external instrument type
    /// </summary>
    /// <param name="message"></param>
    public static InstrumentEnum? InstrumentType(string message)
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
    public static InstrumentModel Instrument(Contract contract)
    {
      var expiration = contract.LastTradeDateOrContractMonth;
      var response = new InstrumentModel() with
      {
        Id = $"{contract.ConId}",
        Name = contract.LocalSymbol,
        Exchange = contract.Exchange,
        Type = InstrumentType(contract.SecType),
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
