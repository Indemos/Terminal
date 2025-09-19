using Core.Common.Enums;
using Core.Common.Grains;
using Core.Common.Implementations;
using Core.Common.States;
using Simulation.Grains;
using Simulation.States;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation
{
  public class SimGateway : Gateway
  {
    /// <summary>
    /// Speed in microsecons
    /// </summary>
    public int Speed { get; init; }

    /// <summary>
    /// Data source
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      var state = new ConnectionState
      {
        Speed = Speed,
        Source = Source,
        Instruments = Account.Instruments
      };

      await ConnectOrders();

      return await Component<IConnectionGrain>().Connect(state);
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override async Task<StatusResponse> Disconnect()
    {
      await DisconnectOrders();

      return await Component<IConnectionGrain>().Disconnect();
    }

    /// <summary>
    /// Subscribe
    /// </summary>
    public override async Task<StatusResponse> Subscribe()
    {
      await Task.WhenAll(Account
        .Instruments
        .Values
        .Select(Subscribe));

      return new StatusResponse
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Unsubscribe
    /// </summary>
    public override async Task<StatusResponse> Unsubscribe()
    {
      await Task.WhenAll(Account
        .Instruments
        .Values
        .Select(Unsubscribe));

      return new StatusResponse
      {
        Data = StatusEnum.Pause
      };
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override async Task<StatusResponse> Subscribe(InstrumentState instrument)
    {
      return await Component<IConnectionGrain>().Subscribe(instrument);
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public override async Task<StatusResponse> Unsubscribe(InstrumentState instrument)
    {
      return await Component<IConnectionGrain>().Unsubscribe(instrument);
    }

    /// <summary>
    /// Get depth of market when available or just a top of the book
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<DomState> Dom(MetaState criteria)
    {
      return await Component<IDomGrain>(criteria.Instrument.Name).Dom(criteria);
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<PriceState>> Bars(MetaState criteria)
    {
      return await Component<ISimPricesGrain>(criteria.Instrument.Name).PriceGroups(criteria);
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<PriceState>> Ticks(MetaState criteria)
    {
      return await Component<ISimPricesGrain>(criteria.Instrument.Name).Prices(criteria);
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<InstrumentState>> Options(MetaState criteria)
    {
      return await Component<ISimOptionsGrain>(criteria.Instrument.Name).Options(criteria);
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<OrderState>> Orders(MetaState criteria)
    {
      return await Component<IOrdersGrain>().Orders(criteria);
    }

    /// <summary>
    /// Get all account positions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<OrderState>> Positions(MetaState criteria)
    {
      return await Component<IPositionsGrain>().Positions(criteria);
    }

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<OrderState>> Transactions(MetaState criteria)
    {
      return await Component<ITransactionsGrain>().Transactions(criteria);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="order"></param>
    public override async Task<OrderGroupsResponse> SendOrder(OrderState order)
    {
      var response = new OrderGroupsResponse();
      var ordersGrain = Component<IOrdersGrain>();

      foreach (var nextOrder in Compose(order))
      {
        var orderResponse = new DescriptorResponse
        {
          Errors = [.. GetErrors(nextOrder).Select(e => e.Message)]
        };

        if (orderResponse.Errors.Count is 0)
        {
          orderResponse = await Component<IOrdersGrain>().Send(nextOrder);
        }

        response.Data.Add(orderResponse);
      }

      return response;
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public override Task<DescriptorResponse> ClearOrder(OrderState order)
    {
      return Component<IOrdersGrain>().Remove(order);
    }
  }
}
