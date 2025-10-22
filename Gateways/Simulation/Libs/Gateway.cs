using Core.Grains;
using Core.Models;
using Simulation.Grains;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Simulation
{
  public class Gateway : Core.Conventions.Gateway
  {
    /// <summary>
    /// Streamer
    /// </summary>
    protected Streamer streamer;

    /// <summary>
    /// Speed in microseconds
    /// </summary>
    public int Speed { get; set; }

    /// <summary>
    /// Data source
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    public override Task<StatusResponse> Connect()
    {
      streamer = new Streamer
      {
        Adapter = this
      };

      ConnectOrders();

      return Task.FromResult(streamer.Connect());
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      return Task.FromResult(streamer.Disconnect());
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Subscribe(InstrumentModel instrument)
    {
      return Task.FromResult(streamer.Subscribe(instrument));
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Unsubscribe(InstrumentModel instrument)
    {
      return Task.FromResult(streamer.Unsubscribe(instrument));
    }

    /// <summary>
    /// Get depth of market when available or just a top of the book
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<DomModel> GetDom(MetaModel criteria)
    {
      return await Component<IDomGrain>(criteria.Instrument.Name).Dom(criteria);
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<PriceModel>> GetTicks(MetaModel criteria)
    {
      return await Component<IGatewayPricesGrain>(criteria.Instrument.Name).Prices(criteria);
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<PriceModel>> GetBars(MetaModel criteria)
    {
      return await Component<IGatewayPricesGrain>(criteria.Instrument.Name).PriceGroups(criteria);
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<InstrumentModel>> GetOptions(MetaModel criteria)
    {
      return await Component<IGatewayOptionsGrain>(criteria.Instrument.Name).Options(criteria);
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<OrderModel>> GetOrders(MetaModel criteria)
    {
      return await Component<IOrdersGrain>().Orders(criteria);
    }

    /// <summary>
    /// Get all account positions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<OrderModel>> GetPositions(MetaModel criteria)
    {
      return await Component<IPositionsGrain>().Positions(criteria);
    }

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<OrderModel>> GetTransactions(MetaModel criteria)
    {
      return await Component<ITransactionsGrain>().Transactions(criteria);
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
