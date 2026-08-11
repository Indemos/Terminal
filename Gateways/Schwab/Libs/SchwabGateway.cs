using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using Schwab.Grains;
using Schwab.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Schwab
{
  public class SchwabGateway : Gateway
  {
    /// <summary>
    /// Client ID
    /// </summary>
    public virtual string ClientId { get; set; }

    /// <summary>
    /// Client secret
    /// </summary>
    public virtual string ClientSecret { get; set; }

    /// <summary>
    /// Access token
    /// </summary>
    public virtual string AccessToken { get; set; }

    /// <summary>
    /// Refresh token
    /// </summary>
    public virtual string RefreshToken { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      await Component<ISchwabConnectionGrain>().Disconnect();

      var observer = Connector.CreateObjectReference<ITradeObserver>(this);
      var connection = new Connection()
      {
        Id = ClientId,
        Secret = ClientSecret,
        RefreshToken = RefreshToken,
        AccessToken = AccessToken,
        Account = Account
      };

      SubscribeToUpdates();

      return await Component<ISchwabConnectionGrain>().Setup(connection, observer);
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      return Component<ISchwabConnectionGrain>().Disconnect();
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override async Task<StatusResponse> Subscribe(Instrument instrument)
    {
      var grain = Component<ISchwabConnectionGrain>();

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
        return Component<ISchwabInstrumentGrain>(criteria.Instrument.Name).Prices(criteria);
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
        return Component<ISchwabInstrumentGrain>(criteria.Instrument.Name).PriceGroups(criteria);
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
        return Component<ISchwabOptionsGrain>(criteria.Instrument.Name).Options(criteria);
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
        var sourceGrain = Component<ISchwabOrdersGrain>();
        var response = await sourceGrain.Orders(criteria);

        await grain.Store(response.Data.ToDictionary(o => o.Operation.Id));

        return response;
      }

      return await base.GetOrders(criteria);
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
        var sourceGrain = Component<ISchwabPositionsGrain>();
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
      return Component<ISchwabOrderSenderGrain>().Send(order);
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(Order order)
    {
      return Component<ISchwabOrderSenderGrain>().Clear(order);
    }
  }
}
