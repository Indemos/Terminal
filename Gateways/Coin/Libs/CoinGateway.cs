using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Coin.Grains;
using Coin.Models;

namespace Coin
{
  public class CoinGateway : Gateway
  {
    /// <summary>
    /// Token
    /// </summary>
    public virtual string Token { get; set; }

    /// <summary>
    /// Secret
    /// </summary>
    public virtual string Secret { get; set; }

    /// <summary>
    /// Exchange
    /// </summary>
    public virtual string Exchange { get; set; }

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
        Token = Token,
        Secret = Secret,
        Account = Account,
        Exchange = Exchange
      };

      SubscribeToUpdates();

      await Component<ICoinOrdersGrain>().Setup(connection);
      await Component<ICoinPositionsGrain>().Setup(connection);
      await Component<ICoinOrderSenderGrain>().Setup(connection);
      await Component<ICoinConnectionGrain>().Setup(connection, observer);

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
      return Component<ICoinConnectionGrain>().Disconnect();
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Subscribe(Instrument instrument)
    {
      return Component<ICoinConnectionGrain>().Subscribe(instrument);
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
      return Component<IOptionsGrain>(criteria.Instrument.Name).Options(criteria);
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetOrders(Criteria criteria)
    {
      var ordersGrain = Component<IOrdersGrain>();
      var connectionGrain = Component<ICoinOrdersGrain>();

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
      var connectionGrain = Component<ICoinPositionsGrain>();

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
      return Component<ITransactionsGrain>().Transactions(criteria);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="order"></param>
    public override Task<OrderResponse> SendOrder(Order order)
    {
      return Component<ICoinOrderSenderGrain>().Send(order);
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(Order order)
    {
      return Component<ICoinOrderSenderGrain>().Clear(order);
    }
  }
}
