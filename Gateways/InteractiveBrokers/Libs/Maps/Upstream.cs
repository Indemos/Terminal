using Core.Enums;
using Core.Models;
using SimpleBroker;
using System.Linq;

namespace InteractiveBrokers.Mappers
{
  public class Upstream
  {
    /// <summary>
    /// Instrument to contract
    /// </summary>
    /// <param name="instrument"></param>
    public static Contract Contract(InstrumentModel instrument)
    {
      var basis = instrument.Basis;
      var derivative = instrument.Derivative;
      var contract = new Contract(
        instrument.Name,
        InstrumentType(instrument.Type),
        instrument.Currency.Name,
        instrument.Exchange)
      {
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
    /// Get external instrument type
    /// </summary>
    /// <param name="message"></param>
    public static string InstrumentType(InstrumentEnum? message)
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
    /// Get price for brackets
    /// </summary>
    /// <param name="order"></param>
    /// <param name="direction"></param>
    public static double? BracePrice(OrderModel order, double direction)
    {
      var nextOrder = order
        .Orders
        .Where(o => Equals(o?.Operation?.Instrument?.Name, order?.Operation?.Instrument?.Name))
        .FirstOrDefault(o => (o.Price - order.Price) * direction > 0);

      return nextOrder?.Price;
    }
  }
}
