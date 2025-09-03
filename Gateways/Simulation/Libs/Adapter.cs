using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Grains;
using Core.Common.Implementations;
using Core.Common.States;
using Simulation.Grains;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation
{
  public class Adapter : Gateway, IDisposable
  {
    /// <summary>
    /// Simulation speed in milliseconds
    /// </summary>
    public virtual int Speed { get; set; } = 1000;

    /// <summary>
    /// Location of the files with quotes
    /// </summary>
    public virtual string Source { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      var descriptor = new Descriptor
      {
        Account = Account.Descriptor
      };

      var grain = Connector.Get<IConnectionGrain>(descriptor);

      await ConnectOrders();
      await grain.StoreInstruments(Account.Instruments);
      await grain.Connect(Source, Speed);

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override async Task<StatusResponse> Disconnect()
    {
      var descriptor = new Descriptor
      {
        Account = Account.Descriptor
      };

      await DisconnectOrders();
      await Connector
        .Get<IConnectionGrain>(descriptor)
        .Disconnect();

      return new()
      {
        Data = StatusEnum.Inactive
      };
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override async Task<StatusResponse> Subscribe(InstrumentState instrument)
    {
      var descriptor = new Descriptor
      {
        Account = Account.Descriptor
      };

      await Connector
        .Get<IConnectionGrain>(descriptor)
        .Subscribe(instrument);

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public override async Task<StatusResponse> Unsubscribe(InstrumentState instrument)
    {
      var descriptor = new Descriptor
      {
        Account = Account.Descriptor
      };

      await Connector
        .Get<IConnectionGrain>(descriptor)
        .Unsubscribe(instrument);

      return new()
      {
        Data = StatusEnum.Pause
      };
    }

    /// <summary>
    /// Get depth of market when available or just a top of the book
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<DomResponse> GetDom(ConditionState criteria = null)
    {
      var descriptor = new InstrumentDescriptor
      {
        Account = Account.Descriptor,
        Instrument = criteria.Instrument.Name
      };

      var domResponse = await Connector
        .Get<IDomGrain>(descriptor)
        .Dom();

      return new DomResponse
      {
        Data = domResponse.Data
      };
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<PricesResponse> GetBars(ConditionState criteria = null)
    {
      var descriptor = new InstrumentDescriptor
      {
        Account = Account.Descriptor,
        Instrument = criteria.Instrument.Name
      };

      var priceResponse = await Connector
        .Get<IPricesGrain>(descriptor)
        .PriceGroups();

      var response = new PricesResponse
      {
        Data = [.. priceResponse
          .Data
          .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate)
          .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate)
          .TakeLast(criteria.Count ?? priceResponse.Data.Count)]
      };

      return response;
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<PricesResponse> GetTicks(ConditionState criteria = null)
    {
      var descriptor = new InstrumentDescriptor
      {
        Account = Account.Descriptor,
        Instrument = criteria.Instrument.Name
      };

      var priceResponse = await Connector
        .Get<IPricesGrain>(descriptor)
        .Prices();

      var response = new PricesResponse
      {
        Data = [.. priceResponse
          .Data
          .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate)
          .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate)
          .TakeLast(criteria.Count ?? priceResponse.Data.Count)]
      };

      return response;
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<InstrumentsResponse> GetOptions(ConditionState criteria = null)
    {
      var descriptor = new InstrumentDescriptor
      {
        Account = Account.Descriptor,
        Instrument = criteria.Instrument.Name
      };

      var optionResponse = await Connector
        .Get<IOptionsGrain>(descriptor)
        .Options();

      var side = criteria
        ?.Instrument
        ?.Derivative
        ?.Side;

      var options = optionResponse
        .Data
        .Where(o => side is null || Equals(o.Derivative.Side, side))
        .Where(o => criteria?.MinDate is null || o.Derivative.ExpirationDate?.Date >= criteria.MinDate?.Date)
        .Where(o => criteria?.MaxDate is null || o.Derivative.ExpirationDate?.Date <= criteria.MaxDate?.Date)
        .Where(o => criteria?.MinPrice is null || o.Derivative.Strike >= criteria.MinPrice)
        .Where(o => criteria?.MaxPrice is null || o.Derivative.Strike <= criteria.MaxPrice)
        .OrderBy(o => o.Derivative.ExpirationDate)
        .ThenBy(o => o.Derivative.Strike)
        .ThenBy(o => o.Derivative.Side)
        //.Select(UpdateInstrument)
        .ToArray();

      var response = new InstrumentsResponse
      {
        Data = options
      };

      return response;
    }

    /// <summary>
    /// Load account data
    /// </summary>
    public override Task<AccountResponse> GetAccount()
    {
      var response = new AccountResponse
      {
        Data = Account
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetOrders(ConditionState criteria = null)
    {
      var descriptor = new Descriptor
      {
        Account = Account.Descriptor
      };

      var ordersGrain = Connector.Get<IOrdersGrain>(descriptor);
      var response = new OrdersResponse
      {
        Data = await ordersGrain.Orders()
      };

      return response;
    }

    /// <summary>
    /// Get all account positions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetPositions(ConditionState criteria = null)
    {
      var descriptor = new Descriptor
      {
        Account = Account.Descriptor
      };

      var positionsGrain = Connector.Get<IPositionsGrain>(descriptor);
      var response = new OrdersResponse
      {
        Data = await positionsGrain.Positions()
      };

      return response;
    }

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetTransactions(ConditionState criteria = null)
    {
      var descriptor = new Descriptor
      {
        Account = Account.Descriptor
      };

      var transactionsGrain = Connector.Get<ITransactionsGrain>(descriptor);
      var response = new OrdersResponse
      {
        Data = await transactionsGrain.Transactions()
      };

      return response;
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="order"></param>
    public override async Task<OrderGroupsResponse> SendOrder(OrderState order)
    {
      var response = new OrderGroupsResponse();

      foreach (var nextOrder in Compose(order))
      {
        var orderResponse = new OrderResponse
        {
          Errors = [.. GetErrors(nextOrder).Select(e => e.Message)]
        };

        if (orderResponse.Errors.Count is 0)
        {
          var descriptor = new OrderDescriptor
          {
            Account = Account.Descriptor,
            Order = order.Id
          };

          await Connector
            .Get<IOrderGrain>(descriptor)
            .StoreOrder(nextOrder);

          orderResponse = orderResponse with { Data = nextOrder };
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
      var descriptor = new Descriptor
      {
        Account = Account.Descriptor
      };

      return Connector
        .Get<IOrdersGrain>(descriptor)
        .Remove(order);
    }
  }
}
