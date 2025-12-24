using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using Schwab.Grains;
using Schwab.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Schwab
{
  public class SchwabGateway : Gateway
  {
    /// <summary>
    /// Client ID
    /// </summary>
    public virtual string ClientId { get; set; }

    /// <summary>
    /// Client secret
    /// </summary>
    public virtual string ClientSecret { get; set; }

    /// <summary>
    /// Access token
    /// </summary>
    public virtual string AccessToken { get; set; }

    /// <summary>
    /// Refresh token
    /// </summary>
    public virtual string RefreshToken { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      SubscribeToUpdates();

      var observer = Connector.CreateObjectReference<ITradeObserver>(this);
      var connection = new Connection()
      {
        Id = ClientId,
        Secret = ClientSecret,
        RefreshToken = RefreshToken,
        AccessToken = AccessToken,
        Account = Account
      };

      await Component<ISchwabConnectionGrain>().Setup(connection, observer);

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
      return Component<ISchwabConnectionGrain>().Disconnect();
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Subscribe(Instrument instrument)
    {
      return Component<ISchwabConnectionGrain>().Subscribe(instrument);
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
      return Component<ISchwabOptionsGrain>(criteria.Instrument.Name).Options(criteria);
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetOrders(Criteria criteria)
    {
      var ordersGrain = Component<IOrdersGrain>();
      var connectionGrain = Component<ISchwabOrdersGrain>();

      if (criteria?.Source is not true)
      {
        return await ordersGrain.Orders(criteria);
      }

      var response = await connectionGrain.Orders(criteria);

      await ordersGrain.Store(response.Data.ToDictionary(o => o.Id));

      return response;
    }

    /// <summary>
    /// Get all account positions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetPositions(Criteria criteria)
    {
      var positionsGrain = Component<IPositionsGrain>();
      var connectionGrain = Component<ISchwabPositionsGrain>();

      if (criteria?.Source is not true)
      {
        return await positionsGrain.Positions(criteria);
      }

      var response = await connectionGrain.Positions(criteria);

      await positionsGrain.Store(response.Data.ToDictionary(o => o.Operation.Instrument.Name));

      return response;
    }

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<OrdersResponse> GetTransactions(Criteria criteria)
    {
      return Component<ISchwabTransactionsGrain>().Transactions(criteria);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="order"></param>
    public override Task<OrderResponse> SendOrder(Order order)
    {
      return Component<ISchwabOrderSenderGrain>().Send(order);
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(Order order)
    {
      return Component<ISchwabOrderSenderGrain>().Clear(order);
    }
  }
}
