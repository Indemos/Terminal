using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using System.Linq;
using System.Threading.Tasks;
using Topstep.Grains;
using Topstep.Models;

namespace Topstep
{
  public class TopstepGateway : Gateway
  {
    /// <summary>
    /// Username
    /// </summary>
    public virtual string Username { get; set; }

    /// <summary>
    /// Access token
    /// </summary>
    public virtual string Token { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      await Component<ITopstepConnectionGrain>().Disconnect();

      var observer = Connector.CreateObjectReference<ITradeObserver>(this);
      var connection = new Connection()
      {
        Token = Token,
        Account = Account,
        Username = Username
      };

      SubscribeToUpdates();

      return await Component<ITopstepConnectionGrain>().Setup(connection, observer);
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      return Component<ITopstepConnectionGrain>().Disconnect();
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Subscribe(Instrument instrument)
    {
      return Component<ITopstepConnectionGrain>().Subscribe(instrument);
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
      return Component<IInstrumentGrain>(criteria.Instrument.Name).Prices(criteria);
    }

    /// <summary>
    /// Bars
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> GetPriceGroups(PriceCriteria criteria)
    {
      return Component<IInstrumentGrain>(criteria.Instrument.Name).PriceGroups(criteria);
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
        var sourceGrain = Component<ITopstepOrdersGrain>();
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
        var sourceGrain = Component<ITopstepPositionsGrain>();
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
      return Component<ITopstepOrderSenderGrain>().Send(order);
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(Order order)
    {
      return Component<ITopstepOrderSenderGrain>().Clear(order);
    }
  }
}
