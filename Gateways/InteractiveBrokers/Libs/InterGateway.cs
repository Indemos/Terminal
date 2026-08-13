using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using InteractiveBrokers.Grains;
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
      await Component<IInterConnectionGrain>().Disconnect();

      var observer = Connector.CreateObjectReference<ITradeObserver>(this);
      var connection = new Connection
      {
        Host = Host,
        Port = Port,
        Span = Span,
        Timeout = Timeout,
        Account = Account
      };

      SubscribeToUpdates();

      return await Component<IInterConnectionGrain>().Setup(connection, observer);
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
    public override async Task<StatusResponse> Subscribe(Instrument instrument)
    {
      var grain = Component<IInterConnectionGrain>();

      await grain.Unsubscribe(instrument);
      await grain.Subscribe(instrument);

      return new StatusResponse { Data = StatusEnum.Active };
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
    /// List of prices
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> GetPrices(PriceCriteria criteria)
    {
      if (criteria.Source)
      {
        return Component<IInterInstrumentGrain>(criteria.Instrument.Name).Prices(criteria);
      }

      return base.GetPrices(criteria);
    }

    /// <summary>
    /// List of aggregated prices
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> GetPriceGroups(PriceCriteria criteria)
    {
      if (criteria.Source)
      {
        return Component<IInterInstrumentGrain>(criteria.Instrument.Name).PriceGroups(criteria);
      }

      return base.GetPriceGroups(criteria);
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<InstrumentsResponse> GetOptions(OptionCriteria criteria)
    {
      if (criteria.Source)
      {
        return Component<IInterOptionsGrain>(criteria.Instrument.Name).Options(criteria);
      }

      return base.GetOptions(criteria);
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetOrders(OrderCriteria criteria)
    {
      var grain = Component<IOrdersGrain>();

      if (criteria.Source)
      {
        var sourceGrain = Component<IInterOrdersGrain>();
        var response = await sourceGrain.Orders(criteria);

        await grain.Store(response.Data.ToDictionary(o => o.Id));

        return response;
      }

      return await base.GetOrders(criteria);
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetPositions(PositionCriteria criteria)
    {
      var grain = Component<IPositionsGrain>();

      if (criteria.Source)
      {
        var sourceGrain = Component<IInterPositionsGrain>();
        var response = await sourceGrain.Positions(criteria);

        await grain.Store(response.Data.ToDictionary(o => o.Operation.Instrument.Name));

        return response;
      }

      return await base.GetPositions(criteria);
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public override async Task<OrderResponse> SendOrder(Order order)
    {
      return await Component<IInterOrderSenderGrain>().SendOrder(order);
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(Order order)
    {
      return Component<IInterOrderSenderGrain>().ClearOrder(order);
    }
  }
}
