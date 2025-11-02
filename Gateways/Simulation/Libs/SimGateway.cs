using Core.Conventions;
using Core.Grains;
using Core.Models;
using Simulation.Grains;
using System.Collections.Generic;
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
      SubscribeToUpdates();

      return await Component<IConnectionGrain>().Connect(new() { Account = Account, Source = Source });
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      return Component<IConnectionGrain>().Disconnect();
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Subscribe(InstrumentModel instrument)
    {
      return Component<IConnectionGrain>().Subscribe(instrument);
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Unsubscribe(InstrumentModel instrument)
    {
      return Component<IConnectionGrain>().Unsubscribe(instrument);
    }

    /// <summary>
    /// Get depth of market when available or just a top of the book
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<DomModel> GetDom(CriteriaModel criteria)
    {
      return Component<IDomGrain>(criteria.Instrument.Name).Dom(criteria);
    }

    /// <summary>
    /// Ticks
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<IList<PriceModel>> GetTicks(CriteriaModel criteria)
    {
      return Component<IGatewayInstrumentGrain>(criteria.Instrument.Name).Prices(criteria);
    }

    /// <summary>
    /// Bars
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<IList<PriceModel>> GetBars(CriteriaModel criteria)
    {
      return Component<IGatewayInstrumentGrain>(criteria.Instrument.Name).PriceGroups(criteria);
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<IList<InstrumentModel>> GetOptions(CriteriaModel criteria)
    {
      return Component<IGatewayOptionsGrain>(criteria.Instrument.Name).Options(criteria);
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<IList<OrderModel>> GetOrders(CriteriaModel criteria)
    {
      return Component<IOrdersGrain>().Orders(criteria);
    }

    /// <summary>
    /// Get all account positions
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<IList<OrderModel>> GetPositions(CriteriaModel criteria)
    {
      return Component<IPositionsGrain>().Positions(criteria);
    }

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<IList<OrderModel>> GetTransactions(CriteriaModel criteria)
    {
      return Component<ITransactionsGrain>().Transactions(criteria);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="order"></param>
    public override Task<OrderGroupsResponse> SendOrder(OrderModel order)
    {
      return Component<IOrdersGrain>().Store(order);
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(OrderModel order)
    {
      return Component<IOrdersGrain>().Clear(order);
    }
  }
}
