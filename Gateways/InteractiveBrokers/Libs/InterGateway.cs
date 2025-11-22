using Core.Conventions;
using Core.Grains;
using Core.Models;
using InteractiveBrokers.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace InteractiveBrokers
{
  public class InterGateway : Gateway
  {
    /// <summary>
    /// Port
    /// </summary>
    public virtual int Port { get; set; } = 7497;

    /// <summary>
    /// Host
    /// </summary>
    public virtual string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Timeout
    /// </summary>
    public virtual TimeSpan Span { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Timeout
    /// </summary>
    public virtual TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      var actionsGrain = Component<ITransactionsGrain>();
      var connectionGrain = Component<IInterConnectionGrain>();
      var observer = Connector.CreateObjectReference<ITradeObserver>(this);

      await actionsGrain.Setup(observer);
      await connectionGrain.Setup(new Connection
      {
        Host = Host,
        Port = Port,
        Span = Span,
        Timeout = Timeout,
        Account = Account,

      }, observer);

      SubscribeToUpdates();

      return await connectionGrain.Connect();
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      return Component<IInterConnectionGrain>().Disconnect();
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Subscribe(Instrument instrument)
    {
      return Component<IInterConnectionGrain>().Subscribe(instrument);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Unsubscribe(Instrument instrument)
    {
      return Component<IInterConnectionGrain>().Unsubscribe(instrument);
    }

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<DomResponse> GetDom(Criteria criteria)
    {
      return Component<IDomGrain>(criteria.Instrument.Name).Dom(criteria);
    }

    /// <summary>
    /// List of prices
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> GetPrices(Criteria criteria)
    {
      var instrumentGrain = Component<IInstrumentGrain>(criteria.Instrument.Name);
      var connectionGrain = Component<IInterConnectionGrain>();

      if (criteria?.Source is not true)
      {
        return instrumentGrain.Prices(criteria);
      }

      return connectionGrain.Prices(criteria);
    }

    /// <summary>
    /// List of aggregated prices
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> GetPriceGroups(Criteria criteria)
    {
      var instrumentGrain = Component<IInstrumentGrain>(criteria.Instrument.Name);
      var connectionGrain = Component<IInterConnectionGrain>();

      if (criteria?.Source is not true)
      {
        return instrumentGrain.PriceGroups(criteria);
      }

      return connectionGrain.PriceGroups(criteria);
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<InstrumentsResponse> GetOptions(Criteria criteria)
    {
      return Component<IInterConnectionGrain>(criteria.Instrument.Name).Options(criteria);
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetOrders(Criteria criteria)
    {
      var ordersGrain = Component<IOrdersGrain>();
      var connectionGrain = Component<IInterConnectionGrain>();

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
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetPositions(Criteria criteria)
    {
      var positionsGrain = Component<IPositionsGrain>();
      var connectionGrain = Component<IInterConnectionGrain>();

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
      return Component<ITransactionsGrain>().Transactions(criteria);
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public override async Task<OrderResponse> SendOrder(Order order)
    {
      var ordersGrain = Component<IOrdersGrain>();
      var connectionGrain = Component<IInterConnectionGrain>();
      var response = await connectionGrain.SendOrder(order);

      await ordersGrain.Send(order);

      return response;
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(Order order)
    {
      return Component<IInterConnectionGrain>().ClearOrder(order);
    }
  }
}
