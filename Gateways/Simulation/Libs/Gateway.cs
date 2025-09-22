using Core.Common.Enums;
using Core.Common.Grains;
using Core.Common.States;
using Simulation.Grains;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation
{
  public class Gateway : Core.Common.Conventions.Gateway
  {
    /// <summary>
    /// Speed in microsecons
    /// </summary>
    public int Speed { get; set; }

    /// <summary>
    /// Data source
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Data source
    /// </summary>
    public Reader Reader { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    public override Task<StatusResponse> Connect()
    {
      Reader = new Reader
      {
        Adapter = this,
        Descriptor = Descriptor()
      };

      ConnectOrders();

      return Task.FromResult(Reader.Connect());
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      DisconnectOrders();

      return Task.FromResult(Reader.Disconnect());
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
    public override Task<StatusResponse> Subscribe(InstrumentState instrument)
    {
      return Task.FromResult(Reader.Subscribe(instrument));
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Unsubscribe(InstrumentState instrument)
    {
      return Task.FromResult(Reader.Unsubscribe(instrument));
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
        var orderResponse = new OrderResponse
        {
          Errors = [.. GetErrors(nextOrder).Select(e => e.Message)]
        };

        if (orderResponse.Errors.Count is 0)
        {
          orderResponse = await Component<IOrdersGrain>().Store(nextOrder);
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
