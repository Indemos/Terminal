using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using System.Linq;
using System.Threading.Tasks;
using Tradier.Grains;
using Tradier.Models;

namespace Tradier
{
  public class TradierGateway : Gateway
  {
    /// <summary>
    /// Access token
    /// </summary>
    public virtual string AccessToken { get; set; }

    /// <summary>
    /// Streaming session token
    /// </summary>
    public virtual string SessionToken { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      await Component<ITradierConnectionGrain>().Disconnect();

      var observer = Connector.CreateObjectReference<ITradeObserver>(this);
      var connection = new Connection()
      {
        Account = Account
      };

      SubscribeToUpdates();

      return await Component<ITradierConnectionGrain>().Setup(connection, observer);
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      return Component<ITradierConnectionGrain>().Disconnect();
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override async Task<StatusResponse> Subscribe(Instrument instrument)
    {
      var grain = Component<ITradierConnectionGrain>();

      await grain.Unsubscribe(instrument);
      await grain.Subscribe(instrument);

      return new StatusResponse { Data = StatusEnum.Active };
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
    /// Ticks
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> GetPrices(PriceCriteria criteria)
    {
      if (criteria.Source)
      {
        return Component<IInstrumentGrain>(criteria.Instrument.Name).Prices(criteria);
      }

      return base.GetPrices(criteria);
    }

    /// <summary>
    /// Bars
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> GetPriceGroups(PriceCriteria criteria)
    {
      if (criteria.Source)
      {
        return Component<IInstrumentGrain>(criteria.Instrument.Name).PriceGroups(criteria);
      }

      return base.GetPriceGroups(criteria);
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<InstrumentsResponse> GetOptions(OptionCriteria criteria)
    {
      if (criteria.Source)
      {
        return Component<ITradierOptionsGrain>(criteria.Instrument.Name).Options(criteria);
      }

      return base.GetOptions(criteria);
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetOrders(OrderCriteria criteria)
    {
      var grain = Component<IOrdersGrain>();

      if (criteria.Source)
      {
        var sourceGrain = Component<ITradierOrdersGrain>();
        var response = await sourceGrain.Orders(criteria);

        await grain.Store(response.Data.ToDictionary(o => o.Id));

        return response;
      }

      return await grain.Orders(criteria);
    }

    /// <summary>
    /// Get all account positions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetPositions(PositionCriteria criteria)
    {
      var grain = Component<IPositionsGrain>();

      if (criteria.Source)
      {
        var sourceGrain = Component<ITradierPositionsGrain>();
        var response = await sourceGrain.Positions(criteria);

        await grain.Store(response.Data.ToDictionary(o => o.Operation.Instrument.Name));

        return response;
      }

      return await grain.Positions(criteria);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="order"></param>
    public override Task<OrderResponse> SendOrder(Order order)
    {
      return Component<ITradierOrderSenderGrain>().Send(order);
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(Order order)
    {
      return Component<ITradierOrderSenderGrain>().Clear(order);
    }
  }
}
