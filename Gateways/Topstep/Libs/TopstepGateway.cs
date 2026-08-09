using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Topstep.Grains;
using Topstep.Models;

namespace Topstep
{
  public class TopstepGateway : Gateway
  {
    /// <summary>
    /// Repeater
    /// </summary>
    protected System.Timers.Timer counter;

    /// <summary>
    /// Username
    /// </summary>
    public virtual string Username { get; set; }

    /// <summary>
    /// Access token
    /// </summary>
    public virtual string Token { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      var observer = Connector.CreateObjectReference<ITradeObserver>(this);
      var connection = new Connection()
      {
        Token = Token,
        Account = Account,
        Username = Username
      };

      SubscribeToUpdates();

      await Component<ITopstepConnectionGrain>().Setup(connection, observer);

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
      return Component<ITopstepConnectionGrain>().Disconnect();
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Subscribe(Instrument instrument)
    {
      return Component<ITopstepConnectionGrain>().Subscribe(instrument);
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
    public override Task<DomResponse> GetDom(DomCriteria criteria)
    {
      return Component<IDomGrain>(criteria.Instrument.Name).Dom(criteria);
    }

    /// <summary>
    /// Ticks
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> GetPrices(PriceCriteria criteria)
    {
      return Component<IInstrumentGrain>(criteria.Instrument.Name).Prices(criteria);
    }

    /// <summary>
    /// Bars
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> GetPriceGroups(PriceCriteria criteria)
    {
      return Component<IInstrumentGrain>(criteria.Instrument.Name).PriceGroups(criteria);
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<InstrumentsResponse> GetOptions(OptionCriteria criteria)
    {
      return Task.FromResult(new InstrumentsResponse());
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetOrders(OrderCriteria criteria)
    {
      var ordersGrain = Component<IOrdersGrain>();
      var connectionGrain = Component<ITopstepOrdersGrain>();

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
    public override async Task<OrdersResponse> GetPositions(PositionCriteria criteria)
    {
      var positionsGrain = Component<IPositionsGrain>();
      var connectionGrain = Component<ITopstepPositionsGrain>();

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
    public override Task<OrdersResponse> GetTransactions(TransactionCriteria criteria)
    {
      return Component<ITopstepTransactionsGrain>().Transactions(criteria);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="order"></param>
    public override Task<OrderResponse> SendOrder(Order order)
    {
      return Component<ITopstepOrderSenderGrain>().Send(order);
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(Order order)
    {
      return Component<ITopstepOrderSenderGrain>().Clear(order);
    }
  }
}
