using Core.Enums;
using Core.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tradier.Enums;
using Tradier.Messages.Trading;
using Tradier.Queries.Trading;

namespace Tradier.Grains
{
  public interface ITradierOrderSenderGrain : ITradierOrdersGrain
  {
    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Send(Order order);
  }

  public class TradierOrderSenderGrain : TradierOrdersGrain, ITradierOrderSenderGrain
  {
    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Send(Order order)
    {
      var message = MapOrder(order);
      var response = null as OrderResponseMessage;
      var cleaner = new CancellationTokenSource(state.Timeout);

      if (order.Orders.Count is 0)
      {
        switch (order.Operation.Instrument.Type)
        {
          case InstrumentEnum.Shares: response = await connector.SendEquityOrder(message, cleaner.Token); break;
          case InstrumentEnum.Options: response = await connector.SendOptionOrder(message, cleaner.Token); break;
        }
      }
      else
      {
        var isBrace = order.Orders.Any(o => o.Instruction is InstructionEnum.Brace);
        var isCombo = order
          .Orders
          .Append(order)
          .Where(o => o?.Amount is not null)
          .Any(o => o?.Operation?.Instrument?.Type is InstrumentEnum.Shares);

        switch (true)
        {
          case true when isBrace: response = await connector.SendOtocoOrder(message, cleaner.Token); break;
          case true when isCombo: response = await connector.SendComboOrder(message, cleaner.Token); break;
          case true when isCombo is false: response = await connector.SendGroupOrder(message, cleaner.Token); break;
        }
      }

      if (Equals(response?.Status?.ToUpper(), "OK"))
      {
        order = order with { Operation = order.Operation with { Id = $"{response?.Id}" } };
      }

      return new()
      {
        Data = order
      };
    }

    /// <summary>
    /// Map order
    /// </summary>
    /// <param name="order"></param>
    protected virtual OpenOrderRequest MapOrder(Order order)
    {
      OpenOrderRequest map(Order o)
      {
        var response = new OpenOrderRequest();

        response.Price = o.Price;
        response.Quantity = o.Amount;
        response.AccountNumber = o.Account.Descriptor;
        response.Duration = MapTimeSpan(o);
        response.Side = MapOrderSide(o);

        if (o?.Operation?.Instrument?.Type is InstrumentEnum.Options)
        {
          response.OptionSymbol = o.Operation.Instrument.Name;
        }

        return response;
      }

      var response = new OpenOrderRequest();

      if (order.Amount is not 0)
      {
        response = map(order);
      }

      foreach (var o in order.Orders)
      {
        response.Legs.Add(map(o));
      }

      return response;
    }

    /// <summary>
    /// Order side
    /// </summary>
    /// <param name="order"></param>
    protected virtual Tradier.Enums.OrderSideEnum? MapOrderSide(Order order)
    {
      switch (order.Side)
      {
        case Core.Enums.OrderSideEnum.Long: return Tradier.Enums.OrderSideEnum.BUY;
        case Core.Enums.OrderSideEnum.Short: return Tradier.Enums.OrderSideEnum.SELL;
      }

      return null;
    }

    /// <summary>
    /// Order type
    /// </summary>
    /// <param name="order"></param>
    protected virtual string MapOrderType(Order order)
    {
      switch (order.Type)
      {
        case Core.Enums.OrderTypeEnum.Stop: return "stop";
        case Core.Enums.OrderTypeEnum.Limit: return "limit";
        case Core.Enums.OrderTypeEnum.StopLimit: return "stop_limit";
      }

      return "market";
    }

    /// <summary>
    /// Convert local time in force to remote
    /// </summary>
    /// <param name="order"></param>
    protected virtual OrderDurationEnum MapTimeSpan(Order order)
    {
      switch (order.TimeSpan)
      {
        case OrderTimeSpanEnum.AM: return OrderDurationEnum.PRE;
        case OrderTimeSpanEnum.PM: return OrderDurationEnum.POST;
        case OrderTimeSpanEnum.GTC: return OrderDurationEnum.GTC;
      }

      return OrderDurationEnum.DAY;
    }
  }
}
