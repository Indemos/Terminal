using Core.Enums;
using Core.Extensions;
using Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tradier.Messages.Trading;
using Tradier.Queries.Trading;
using Ens = Tradier.Enums;

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
      var descriptor = this.GetDescriptor();
      var positionsGrain = GrainFactory.GetGrain<ITradierPositionsGrain>(descriptor);
      var positionsResponse = await positionsGrain.Positions(new() { Source = true });
      var message = MapOrder(order, positionsResponse.Data.ToDictionary(o => o.Operation.Instrument.Name));
      var cleaner = new CancellationTokenSource(state.Timeout);
      var response = null as OrderResponseMessage;

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
    /// <param name="positions"></param>
    protected virtual OpenOrderRequest MapOrder(Order order, Dictionary<string, Order> positions)
    {
      OpenOrderRequest map(Order o)
      {
        var response = new OpenOrderRequest
        {
          Price = o.Price,
          Quantity = o.Amount,
          AccountNumber = o.Account.Descriptor,
          Side = MapSide(o, positions),
          Duration = MapTimeSpan(o),
          Type = MapType(o)
        };

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
    /// <param name="positions"></param>
    protected virtual Ens.OrderSideEnum? MapSide(Order order, Dictionary<string, Order> positions)
    {
      var position = positions.Get(order?.Operation?.Instrument?.Name);
      var option = order?.Operation?.Instrument?.Type is InstrumentEnum.Options or InstrumentEnum.FutureOptions;

      if (option)
      {
        switch (true)
        {
          case true when position is null && order.Side is OrderSideEnum.Long:
          case true when position.Side is OrderSideEnum.Long && order.Side is OrderSideEnum.Long: return Ens.OrderSideEnum.BUY_TO_OPEN;
          case true when position.Side is OrderSideEnum.Short && order.Side is OrderSideEnum.Long: return Ens.OrderSideEnum.BUY_TO_CLOSE;
          case true when position is null && order.Side is OrderSideEnum.Short:
          case true when position.Side is OrderSideEnum.Short && order.Side is OrderSideEnum.Short: return Ens.OrderSideEnum.SELL_TO_OPEN;
          case true when position.Side is OrderSideEnum.Long && order.Side is OrderSideEnum.Short: return Ens.OrderSideEnum.SELL_TO_CLOSE;
        }
      }

      switch (true)
      {
        case true when position is null && order.Side is OrderSideEnum.Long:
        case true when position.Side is OrderSideEnum.Long && order.Side is OrderSideEnum.Long: return Ens.OrderSideEnum.BUY;
        case true when position.Side is OrderSideEnum.Short && order.Side is OrderSideEnum.Long: return Ens.OrderSideEnum.BUY_TO_COVER;
        case true when position is null && order.Side is OrderSideEnum.Short:
        case true when position.Side is OrderSideEnum.Short && order.Side is OrderSideEnum.Short: return Ens.OrderSideEnum.SELL_SHORT;
        case true when position.Side is OrderSideEnum.Long && order.Side is OrderSideEnum.Short: return Ens.OrderSideEnum.SELL;
      }

      return null;
    }

    /// <summary>
    /// Order type
    /// </summary>
    /// <param name="order"></param>
    protected virtual Ens.OrderTypeEnum MapType(Order order)
    {
      switch (order.Type)
      {
        case OrderTypeEnum.Stop: return Ens.OrderTypeEnum.STOP;
        case OrderTypeEnum.Limit: return Ens.OrderTypeEnum.LIMIT;
        case OrderTypeEnum.StopLimit: return Ens.OrderTypeEnum.STOP_LIMIT;
      }

      return Ens.OrderTypeEnum.MARKET;
    }

    /// <summary>
    /// Convert local time in force to remote
    /// </summary>
    /// <param name="order"></param>
    protected virtual Ens.OrderDurationEnum MapTimeSpan(Order order)
    {
      switch (order.TimeSpan)
      {
        case OrderTimeSpanEnum.AM: return Ens.OrderDurationEnum.PRE;
        case OrderTimeSpanEnum.PM: return Ens.OrderDurationEnum.POST;
        case OrderTimeSpanEnum.GTC: return Ens.OrderDurationEnum.GTC;
      }

      return Ens.OrderDurationEnum.DAY;
    }
  }
}
