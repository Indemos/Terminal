using Core.Enums;
using Core.Models;
using IBApi;
using System.Linq;

namespace InteractiveBrokers.Mappers
{
  public class Upstream
  {
    /// <summary>
    /// Convert remote order from brokerage to local record
    /// </summary>
    /// <param name="orderModel"></param>
    /// <param name="account"></param>
    public static (IBApi.Order, double?, double?) MapOrder(Core.Models.Order orderModel, Account account)
    {
      var order = new IBApi.Order
      {
        Action = MapSide(orderModel.Side),
        Tif = MapTimeSpan(orderModel.TimeSpan),
        OrderType = MapOrderType(orderModel.Type),
        TotalQuantity = (decimal)orderModel.Amount,
        ExtOperator = orderModel.Descriptor,
        Account = account.Descriptor
      };

      switch (orderModel.Type)
      {
        case OrderTypeEnum.Stop: order.AuxPrice = orderModel.Price.Value; break;
        case OrderTypeEnum.Limit: order.LmtPrice = orderModel.Price.Value; break;
        case OrderTypeEnum.StopLimit:
          order.LmtPrice = orderModel.Price.Value;
          order.AuxPrice = orderModel.ActivationPrice.Value;
          break;
      }

      var TP = MapBracePrice(orderModel, orderModel.Side is OrderSideEnum.Long ? 1 : -1);
      var SL = MapBracePrice(orderModel, orderModel.Side is OrderSideEnum.Long ? -1 : 1);

      return (order, TP, SL);
    }

    /// <summary>
    /// Get price for brackets
    /// </summary>
    /// <param name="order"></param>
    /// <param name="direction"></param>
    public static double? MapBracePrice(Core.Models.Order order, double direction)
    {
      var nextOrder = order
        .Orders
        .Where(o => o.Instruction is InstructionEnum.Brace)
        .Where(o => Equals(o.Operation.Instrument.Name, order.Operation.Instrument.Name))
        .FirstOrDefault(o => (o.Price - order.Price) * direction > 0);

      return nextOrder?.Price;
    }

    /// <summary>
    /// Instrument to contract
    /// </summary>
    /// <param name="instrument"></param>
    public static Contract MapContract(Instrument instrument)
    {
      var basis = instrument.Basis;
      var derivative = instrument.Derivative;
      var contract = new Contract
      { 
        Symbol = instrument.Name,
        SecType = MapInstrumentType(instrument.Type),
        Currency = instrument.Currency.Name,
        Exchange = instrument.Exchange,
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
    public static string MapInstrumentType(InstrumentEnum? message)
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
    /// Convert local time in force to remote
    /// </summary>
    /// <param name="span"></param>
    public static string MapTimeSpan(OrderTimeSpanEnum? span)
    {
      switch (span)
      {
        case OrderTimeSpanEnum.DAY: return "DAY";
        case OrderTimeSpanEnum.GTC: return "GTC";
      }

      return null;
    }

    /// <summary>
    /// Order side
    /// </summary>
    /// <param name="order"></param>
    public static string MapSide(OrderSideEnum? side)
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
    public static string MapOrderType(OrderTypeEnum? message)
    {
      switch (message)
      {
        case OrderTypeEnum.Stop: return "STP";
        case OrderTypeEnum.Limit: return "LMT";
        case OrderTypeEnum.StopLimit: return "STP LMT";
      }

      return "MKT";
    }
  }
}
