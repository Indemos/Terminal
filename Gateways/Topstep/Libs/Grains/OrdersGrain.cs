using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Topstep.Models;
using TopstepX;
using TopstepX.Models.Orders;

namespace Topstep.Grains
{
  public interface ITopstepOrdersGrain : IOrdersGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);

    /// <summary>
    /// Validate session token
    /// </summary>
    /// <param name="session"></param>
    Task<StatusResponse> Validate(string session);
  }

  public class TopstepOrdersGrain : OrdersGrain, ITopstepOrdersGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected TopstepBroker connector;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;
      connector = new(connection.Username, connection.Token);

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Validate session token
    /// </summary>
    /// <param name="session"></param>
    public virtual async Task<StatusResponse> Validate(string session)
    {
      connector.SetAuthHeader(session);

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> Orders(OrderCriteria criteria)
    {
      var cts = new CancellationTokenSource(state.Timeout);
      var query = new SearchOrderRequest { accountId = int.Parse(criteria.Account.Descriptor) };
      var response = await connector.OrderSearch(query);
      var items = response.orders.Select(MapOrder);

      return new()
      {
        Data = [.. items]
      };
    }

    /// <summary>
    /// Map order
    /// </summary>
    /// <param name="message"></param>
    protected virtual Order MapOrder(OrderModel message)
    {
      var instrument = new Instrument
      {
        Id = message.contractId,
        Name = message.symbolId,
        Type = InstrumentEnum.Futures
      };

      var action = new Operation
      {
        Amount = message.size,
        Time = message.updateTimestamp?.Ticks,
        Status = OrderStatusEnum.Order,
        Instrument = instrument
      };

      var order = new Order
      {
        Operation = action,
        Id = $"{message.id}",
        Type = OrderTypeEnum.Market,
        Amount = message.size,
        Side = MapSide(message)
      };

      switch (message?.type)
      {
        case OrderType.Limit: order = order with { Type = OrderTypeEnum.Limit, Price = message.limitPrice }; break;
        case OrderType.Stop: order = order with { Type = OrderTypeEnum.Stop, Price = message.stopPrice }; break;
        case OrderType.StopLimit:

          order = order with
          {
            Price = message.limitPrice,
            Type = OrderTypeEnum.StopLimit,
            ActivationPrice = message.stopPrice
          };

          break;
      }

      return order;
    }

    /// <summary>
    /// Map side
    /// </summary>
    /// <param name="message"></param>
    protected virtual OrderSideEnum? MapSide(OrderModel message)
    {
      switch (message.side)
      {
        case OrderSide.Buy: return OrderSideEnum.Long;
        case OrderSide.Sell: return OrderSideEnum.Short;
      }

      return null;
    }
  }
}
