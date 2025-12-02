using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using Simulation.Grains;
using System.Threading.Tasks;

namespace Simulation
{
  public class SimGateway : Gateway
  {
    /// <summary>
    /// Source
    /// </summary>
    public virtual string Source { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      var observer = Connector.CreateObjectReference<ITradeObserver>(this);
      var connection = new Models.Connection()
      {
        Account = Account,
        Source = Source
      };

      SubscribeToUpdates();

      await Component<ITransactionsGrain>().Setup(observer);
      await Component<ISimConnectionGrain>().Setup(connection, observer);

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
      return Component<ISimConnectionGrain>().Disconnect();
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Subscribe(Instrument instrument)
    {
      return Component<ISimConnectionGrain>().Subscribe(instrument);
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Unsubscribe(Instrument instrument)
    {
      return Component<ISimConnectionGrain>().Unsubscribe(instrument);
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
    public override Task<OrdersResponse> GetOrders(Criteria criteria)
    {
      return Component<ISimOrdersGrain>().Orders(criteria);
    }

    /// <summary>
    /// Get all account positions
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<OrdersResponse> GetPositions(Criteria criteria)
    {
      return Component<ISimPositionsGrain>().Positions(criteria);
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
      return Component<ISimOrdersGrain>().Send(order with { Account = Account });
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(Order order)
    {
      return Component<ISimOrdersGrain>().Clear(order);
    }
  }
}
