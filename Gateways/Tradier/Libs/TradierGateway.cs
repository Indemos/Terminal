using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Tradier.Grains;
using Tradier.Models;

namespace Tradier
{
  public class TradierGateway : Gateway
  {
    /// <summary>
    /// Access token
    /// </summary>
    public virtual string AccessToken { get; set; }

    /// <summary>
    /// Streaming session token
    /// </summary>
    public virtual string SessionToken { get; set; }

    /// <summary>
    /// Timeout
    /// </summary>
    public virtual TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      var observer = Connector.CreateObjectReference<ITradeObserver>(this);
      var connection = new Connection()
      {
        Account = Account
      };

      SubscribeToUpdates();

      await Component<ITradierOrdersGrain>().Setup(connection);
      await Component<ITradierOptionsGrain>().Setup(connection);
      await Component<ITradierPositionsGrain>().Setup(connection);
      await Component<ITradierOrderSenderGrain>().Setup(connection);
      await Component<ITradierConnectionGrain>().Setup(connection, observer);
      await Component<ITradierTransactionsGrain>().Setup(connection, observer);

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      return Component<ITradierConnectionGrain>().Disconnect();
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Subscribe(Instrument instrument)
    {
      return Component<ITradierConnectionGrain>().Subscribe(instrument);
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Unsubscribe(Instrument instrument)
    {
      return Task.FromResult(new StatusResponse { Data = StatusEnum.Pause });
    }

    /// <summary>
    /// Get depth of market when available or just a top of the book
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<DomResponse> GetDom(Criteria criteria)
    {
      return Component<IDomGrain>(criteria.Instrument.Name).Dom(criteria);
    }

    /// <summary>
    /// Ticks
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> GetPrices(Criteria criteria)
    {
      return Component<IInstrumentGrain>(criteria.Instrument.Name).Prices(criteria);
    }

    /// <summary>
    /// Bars
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> GetPriceGroups(Criteria criteria)
    {
      return Component<IInstrumentGrain>(criteria.Instrument.Name).PriceGroups(criteria);
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<InstrumentsResponse> GetOptions(Criteria criteria)
    {
      return Component<ITradierOptionsGrain>(criteria.Instrument.Name).Options(criteria);
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetOrders(Criteria criteria)
    {
      var ordersGrain = Component<IOrdersGrain>();
      var connectionGrain = Component<ITradierOrdersGrain>();

      if (criteria?.Source is not true)
      {
        return await ordersGrain.Orders(criteria);
      }

      var response = await connectionGrain.Orders(criteria);

      await ordersGrain.Clear();
      await Task.WhenAll(response.Data.Select(ordersGrain.Send));

      return response;
    }

    /// <summary>
    /// Get all account positions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetPositions(Criteria criteria)
    {
      var positionsGrain = Component<IPositionsGrain>();
      var connectionGrain = Component<ITradierPositionsGrain>();

      if (criteria?.Source is not true)
      {
        return await positionsGrain.Positions(criteria);
      }

      var response = await connectionGrain.Positions(criteria);

      await positionsGrain.Clear();
      await Task.WhenAll(response.Data.Select(positionsGrain.Send));

      return response;
    }

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<OrdersResponse> GetTransactions(Criteria criteria)
    {
      return Component<ITradierTransactionsGrain>().Transactions(criteria);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="order"></param>
    public override Task<OrderResponse> SendOrder(Order order)
    {
      return Component<ITradierOrderSenderGrain>().Send(order);
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(Order order)
    {
      return Component<ITradierOrderSenderGrain>().Clear(order);
    }
  }
}
