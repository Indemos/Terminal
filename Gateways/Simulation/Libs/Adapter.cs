using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Grains;
using Core.Common.Services;
using Core.Common.States;
using Core.Common.Validators;
using Simulation.Grains;
using System;
using System.Collections.Generic;
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
      var grain = GrainFactory.GetGrain<ConnectionGrain>(Account.Descriptor);

      await grain.StoreInstruments(Account.Instruments);
      await grain.Connect(Source, Speed);

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Subscribe(InstrumentState instrument) => GrainFactory
      .GetGrain<ConnectionGrain>(Account.Descriptor)
      .Subscribe(instrument);

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Unsubscribe(InstrumentState instrument) => GrainFactory
      .GetGrain<ConnectionGrain>(Account.Descriptor)
      .Unsubscribe(instrument);

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<StatusResponse> Disconnect() => GrainFactory
      .GetGrain<ConnectionGrain>(Account.Descriptor)
      .Disconnect();

    /// <summary>
    /// Get depth of market when available or just a top of the book
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<DomResponse> GetDom(ConditionState criteria = null)
    {
      var instrumentName = criteria.Instrument.Name;
      var domGrain = GrainFactory.GetGrain<DomGrain>($"{Account.Descriptor}:{instrumentName}");
      var domResponse = await domGrain.Get();

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
      var instrumentName = criteria.Instrument.Name;
      var pricesGrain = GrainFactory.GetGrain<PricesGrain>($"{Account.Descriptor}:{instrumentName}");
      var pointGroupResponse = await pricesGrain.GetPriceGroups();
      var response = new PricesResponse
      {
        Data = [.. pointGroupResponse
          .Data
          .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate)
          .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate)]
      };

      return response;
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<PricesResponse> GetTicks(ConditionState criteria = null)
    {
      var instrumentName = criteria.Instrument.Name;
      var pricesGrain = GrainFactory.GetGrain<PricesGrain>($"{Account.Descriptor}:{instrumentName}");
      var pointResponse = await pricesGrain.GetPrices();
      var response = new PricesResponse
      {
        Data = [.. pointResponse
          .Data
          .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate)
          .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate)]
      };

      return response;
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<InstrumentsResponse> GetOptions(ConditionState criteria = null)
    {
      var instrumentName = criteria.Instrument.Name;
      var optionsGrain = GrainFactory.GetGrain<OptionsGrain>($"{Account.Descriptor}:{instrumentName}");
      var optionResponse = await optionsGrain.Get();
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
        .ToList();

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
      var response = new OrdersResponse();
      var ordersGrain = GrainFactory.GetGrain<OrdersGrain>(Account.Descriptor);
      var count = await ordersGrain.Count();

      for (var i = 0; i < count; i++)
      {
        var orderGrain = await ordersGrain.Get(i);
        response.Data.Add(await orderGrain.Get());
      }

      return response;
    }

    /// <summary>
    /// Get all account positions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetPositions(ConditionState criteria = null)
    {
      var response = new OrdersResponse();
      var positionsGrain = GrainFactory.GetGrain<PositionsGrain>(Account.Descriptor);
      var count = await positionsGrain.Count();

      for (var i = 0; i < count; i++)
      {
        var positionGrain = await positionsGrain.Get(i);
        response.Data.Add(await positionGrain.Get());
      }

      return response;
    }

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> GetTransactions(ConditionState criteria = null)
    {
      var response = new OrdersResponse();
      var transactionsGrain = GrainFactory.GetGrain<TransactionsGrain>(Account.Descriptor);
      var count = await transactionsGrain.Count();

      for (var i = 0; i < count; i++)
      {
        var transactionGrain = await transactionsGrain.Get(i);
        response.Data.Add(await transactionGrain.Get());
      }

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
          await GrainFactory
            .GetGrain<OrderGrain>($"{Account.Descriptor}:{order.Id}")
            .Send(nextOrder);

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
    public override Task<DescriptorResponse> ClearOrder(OrderState order) => GrainFactory
      .GetGrain<OrdersGrain>(Account.Descriptor)
      .Remove(order);
  }
}
