using Core.Conventions;
using Core.Grains;
using Core.Models;
using InteractiveBrokers.Models;
using System;
using System.Collections.Generic;
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
    public virtual TimeSpan Span { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Timeout
    /// </summary>
    public virtual TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      var grain = Component<IConnectionGrain>();

      await grain.Store(new ConnectionModel
      {
        Host = Host,
        Port = Port,
        Span = Span,
        Timeout = Timeout,
        Account = Account,
      });

      SubscribeToUpdates();

      return await grain.Connect();
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
    /// Unsubscribe from data streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Unsubscribe(InstrumentModel instrument)
    {
      return Component<IConnectionGrain>().Unsubscribe(instrument);
    }

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<DomModel> GetDom(CriteriaModel criteria)
    {
      return Component<IDomGrain>(criteria.Instrument.Name).Dom(criteria);
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<IList<PriceModel>> GetTicks(CriteriaModel criteria)
    {
      return Component<IInstrumentGrain>(criteria.Instrument.Name).Prices(criteria);
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<IList<PriceModel>> GetBars(CriteriaModel criteria)
    {
      return Component<IInstrumentGrain>(criteria.Instrument.Name).PriceGroups(criteria);
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<IList<InstrumentModel>> GetOptions(CriteriaModel criteria)
    {
      return Component<IConnectionGrain>(criteria.Instrument.Name).Options(criteria);
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<OrderModel>> GetOrders(CriteriaModel criteria)
    {
      var ordersGrain = Component<IOrdersGrain>();
      var connectionGrain = Component<IConnectionGrain>();
      var response = await connectionGrain.Orders(criteria);

      await ordersGrain.Clear();

      foreach (var order in response)
      {
        await ordersGrain.Store(order);
      }

      return response;
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<OrderModel>> GetPositions(CriteriaModel criteria)
    {
      var positionsGrain = Component<IPositionsGrain>();
      var connectionGrain = Component<IConnectionGrain>();
      var response = await positionsGrain.Positions(criteria);

      await positionsGrain.Clear();

      foreach (var order in response)
      {
        await positionsGrain.Store(order);
      }

      return response;
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
    /// Send order
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
