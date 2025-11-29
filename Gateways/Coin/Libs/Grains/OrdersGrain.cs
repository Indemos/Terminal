using Coin.Models;
using Core.Enums;
using Core.Grains;
using Core.Models;
using CryptoClients.Net;
using CryptoClients.Net.Interfaces;
using CryptoExchange.Net.SharedApis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Coin.Grains
{
  public interface ICoinOrdersGrain : IOrdersGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(Connection connection);
  }

  public class CoinOrdersGrain : OrdersGrain, ICoinOrdersGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected IExchangeRestClient sender;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection)
    {
      var cleaner = new CancellationTokenSource(connection.Timeout);

      state = connection;
      sender = new ExchangeRestClient();

      sender.SetApiCredentials(state.Exchange, state.Token, state.Secret);

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> Orders(Criteria criteria)
    {
      var spotCleaner = new CancellationTokenSource(state.Timeout);
      var futureCleaner = new CancellationTokenSource(state.Timeout);
      var query = new CryptoExchange.Net.SharedApis.GetOpenOrdersRequest();
      var spotResponse = await sender.GetSpotOpenOrdersAsync(state.Exchange, query);
      var futureResponse = await sender.GetFuturesOpenOrdersAsync(state.Exchange, query);
      var spotItems = spotResponse.Data.Select(MapSpotOrder);
      var futureItems = futureResponse.Data.Select(MapFutureOrder);

      return new()
      {
        Data = [.. spotItems, .. futureItems]
      };
    }

    /// <summary>
    /// Map order
    /// </summary>
    /// <param name="message"></param>
    protected virtual Order MapSpotOrder(SharedSpotOrder message)
    {
      var instrument = new Instrument
      {
        Type = InstrumentEnum.Coins,
        Name = message.Symbol
      };

      var action = new Operation
      {
        Id = $"{message.OrderId}",
        Amount = (double?)message.QuantityFilled.QuantityInContracts,
        Time = message.UpdateTime?.Ticks,
        Status = OrderStatusEnum.Order,
        Instrument = instrument
      };

      var order = new Order
      {
        Operation = action,
        Id = message.ClientOrderId,
        Type = OrderTypeEnum.Market,
        Price = (double?)message.OrderPrice,
        Amount = (double?)message.OrderQuantity.QuantityInContracts,
        Side = MapSide(message.Side)
      };

      if (message.OrderType is SharedOrderType.Limit or SharedOrderType.LimitMaker)
      {
        order = order with { Type = OrderTypeEnum.Limit };
      }

      if (message.IsTriggerOrder is true)
      {
        order = order with { Type = OrderTypeEnum.StopLimit, Price = (double?)message.TriggerPrice };
      }

      return order;
    }

    /// <summary>
    /// Map order
    /// </summary>
    /// <param name="message"></param>
    protected virtual Order MapFutureOrder(SharedFuturesOrder message)
    {
      var instrument = new Instrument
      {
        Leverage = (double?)message.Leverage,
        Type = InstrumentEnum.Futures,
        Name = message.Symbol
      };

      var action = new Operation
      {
        Id = $"{message.OrderId}",
        Amount = (double?)message.QuantityFilled.QuantityInContracts,
        Time = message.UpdateTime?.Ticks,
        Status = OrderStatusEnum.Order,
        Instrument = instrument
      };

      var order = new Order
      {
        Operation = action,
        Id = message.ClientOrderId,
        Type = OrderTypeEnum.Market,
        Price = (double?)message.OrderPrice,
        Amount = (double?)message.OrderQuantity.QuantityInContracts,
        Side = MapSide(message.Side)
      };

      if (message.OrderType is SharedOrderType.Limit or SharedOrderType.LimitMaker)
      {
        order = order with { Type = OrderTypeEnum.Limit };
      }

      if (message.IsTriggerOrder is true)
      {
        order = order with { Type = OrderTypeEnum.StopLimit, Price = (double?)message.TriggerPrice };
      }

      return order;
    }

    protected virtual OrderSideEnum? MapSide(SharedOrderSide side)
    {
      switch (side)
      {
        case SharedOrderSide.Buy: return OrderSideEnum.Long;
        case SharedOrderSide.Sell: return OrderSideEnum.Short;
      }

      return null;
    }
  }
}
