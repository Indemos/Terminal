using Core.Grains;
using Core.Models;
using InteractiveBrokers.Mappers;
using InteractiveBrokers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InteractiveBrokers
{
  public class Gateway : Core.Conventions.Gateway
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
    /// Price update
    /// </summary>
    /// <param name="price"></param>
    public virtual Task OnPrice(PriceModel price) => Subscription(price);

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

      ConnectPrices();
      ConnectOrders();

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
    public override async Task<StatusResponse> Unsubscribe(InstrumentModel instrument)
    {
      return await Component<IConnectionGrain>().Unsubscribe(instrument);
    }

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<DomModel> Dom(MetaModel criteria)
    {
      return await Component<IDomGrain>(criteria.Instrument.Name).Dom(criteria);
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<PriceModel>> Ticks(MetaModel criteria)
    {
      return await Component<IPricesGrain>(criteria.Instrument.Name).Prices(criteria);
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<PriceModel>> Bars(MetaModel criteria)
    {
      return await Component<IPricesGrain>(criteria.Instrument.Name).PriceGroups(criteria);
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<InstrumentModel>> Options(MetaModel criteria)
    {
      return await Component<IConnectionGrain>(criteria.Instrument.Name).Options(criteria);
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<OrderModel>> Orders(MetaModel criteria)
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
    public override async Task<IList<OrderModel>> Positions(MetaModel criteria)
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
    public override Task<IList<OrderModel>> Transactions(MetaModel criteria)
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
