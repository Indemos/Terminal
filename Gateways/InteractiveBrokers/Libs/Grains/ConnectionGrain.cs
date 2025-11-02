using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using Core.Services;
using IBApi;
using IBApi.Messages;
using InteractiveBrokers.Mappers;
using InteractiveBrokers.Models;
using Orleans;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveBrokers
{
  public interface IConnectionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Store(ConnectionModel connection);

    /// <summary>
    /// Connect
    /// </summary>
    Task<StatusResponse> Connect();

    /// <summary>
    /// Save state and dispose
    /// </summary>
    Task<StatusResponse> Disconnect();

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Subscribe(InstrumentModel instrument);

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Unsubscribe(InstrumentModel instrument);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> Bars(CriteriaModel criteria);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> Ticks(CriteriaModel criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Orders(CriteriaModel criteria);

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Positions(CriteriaModel criteria);

    /// <summary>
    /// Get contract definitions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<InstrumentModel>> Options(CriteriaModel criteria);

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    Task<AccountModel> AccountSummary();
  }

  public class ConnectionGrain : Grain<ConnectionModel>, IConnectionGrain
  {
    /// <summary>
    /// IB client
    /// </summary>
    protected InterBroker connector;

    /// <summary>
    /// Stream
    /// </summary>
    protected MessageService messenger;

    /// <summary>
    /// Asset subscriptions
    /// </summary>
    protected ConcurrentDictionary<string, int> subscriptions = new();

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="streamService"></param>
    public ConnectionGrain(MessageService streamService) => messenger = streamService;

    /// <summary>
    /// Deactivation
    /// </summary>
    /// <param name="reason"></param>
    /// <param name="cleaner"></param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cleaner)
    {
      await Disconnect();
      await base.OnActivateAsync(cleaner);
    }

    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    public virtual Task<StatusResponse> Store(ConnectionModel connection)
    {
      State = connection;

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }

    /// <summary>
    /// Connect
    /// </summary>
    public virtual async Task<StatusResponse> Connect()
    {
      await Disconnect();

      connector = new InterBroker { Port = State.Port };
      connector.Connect();

      foreach (var instrument in State.Account.Instruments.Values)
      {
        await Subscribe(instrument);
      }

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public virtual Task<StatusResponse> Disconnect()
    {
      connector?.Disconnect();

      return Task.FromResult(new StatusResponse()
      {
        Data = StatusEnum.Inactive
      });
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public virtual async Task<StatusResponse> Subscribe(InstrumentModel instrument)
    {
      await Unsubscribe(instrument);

      var contract = Upstream.Contract(instrument);
      var cleaner = new CancellationTokenSource(State.Timeout);
      var contracts = await connector.GetContracts(cleaner.Token, contract);
      var contractMessage = contracts.FirstOrDefault();

      if (contractMessage is null)
      {
        await messenger.Send(new MessageModel()
        {
          Content = "No such instrument",
          Action = ActionEnum.Disconnect
        });

        return new()
        {
          Data = StatusEnum.Inactive
        };
      }

      var name = this.GetPrimaryKeyString();
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(name);
      var positionsGrain = GrainFactory.GetGrain<IPositionsGrain>(name);
      var instrumentGrain = GrainFactory.GetGrain<IInstrumentGrain>($"{name}:{instrument.Name}");
      var dataMessage = new DataStreamMessage
      {
        DataTypes = [IBApi.Enums.SubscriptionEnum.Price],
        Contract = contract
      };

      subscriptions[instrument.Name] = connector.SubscribeToTicks(dataMessage, async priceMessage =>
      {
        var price = Downstream.Price(priceMessage);
        var group = await instrumentGrain.Store(instrument with { Price = price });

        await ordersGrain.Tap(group);
        await positionsGrain.Tap(group);

        await messenger.Send(group);
      });

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task<StatusResponse> Unsubscribe(InstrumentModel instrument)
    {
      if (subscriptions.TryRemove(instrument.Name, out var subscription))
      {
        connector.Unsubscribe(subscription);
      }

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Pause
      });
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderModel>> Orders(CriteriaModel criteria)
    {
      var cleaner = new CancellationTokenSource(State.Timeout);
      var sourceItems = await connector.GetOrders(cleaner.Token);
      var items = sourceItems.Select(Downstream.Order).ToArray();

      await Task.Delay(State.Span);

      return items;
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderModel>> Positions(CriteriaModel criteria)
    {
      var cleaner = new CancellationTokenSource(State.Timeout);
      var sourceItems = await connector.GetPositions(cleaner.Token, State.Account.Name);
      var items = sourceItems.Select(o => Downstream.Position(o, criteria.Instrument)).ToArray();

      await Task.Delay(State.Span);

      return items;
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<PriceModel>> Ticks(CriteriaModel criteria)
    {
      var contract = Upstream.Contract(criteria.Instrument);
      var cleaner = new CancellationTokenSource(State.Timeout);
      var sourceItems = await connector.GetTicks(
        cleaner.Token,
        contract,
        criteria.MinDate.Value,
        criteria.MaxDate.Value,
        "BID_ASK",
        criteria.Count ?? 1);

      var items = sourceItems.Select(Downstream.Price).ToArray();

      await Task.Delay(State.Span);

      return items;
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<PriceModel>> Bars(CriteriaModel criteria)
    {
      var cleaner = new CancellationTokenSource(State.Timeout);
      var contract = Upstream.Contract(criteria.Instrument);
      var maxDate = criteria.MaxDate ?? DateTime.Now;
      var sourceItems = await connector.GetBars(
        cleaner.Token,
        contract,
        maxDate,
        criteria.Data.Get("Duration"),
        criteria.Data.Get("BarType"),
        criteria.Data.Get("DataType"),
        0);

      var items = sourceItems.Select(Downstream.Price).ToArray();

      await Task.Delay(State.Span);

      return items;
    }

    /// <summary>
    /// List options
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<InstrumentModel>> Options(CriteriaModel criteria)
    {
      var instrument = criteria.Instrument;
      var minDate = criteria.MinDate?.ToString($"yyyyMMdd-HH:mm:ss");
      var maxDate = (criteria.MaxDate ?? DateTime.Now).ToString($"yyyyMMdd-HH:mm:ss");
      var contract = Upstream.Contract(criteria.Instrument);
      var cleaner = new CancellationTokenSource(State.Timeout);
      var sourceItems = await connector.GetContracts(cleaner.Token, contract);
      var items = sourceItems.Select(o => Downstream.Instrument(o.Contract)).ToArray();

      await Task.Delay(State.Span);

      return items;
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    public virtual async Task<AccountModel> AccountSummary()
    {
      var account = new AccountModel();
      var cleaner = new CancellationTokenSource(State.Timeout);
      var message = await connector.GetAccountSummary(cleaner.Token);

      account = account with { Balance = double.Parse(message.Get("NetLiquidation")) };

      await Task.Delay(State.Span);

      return account;
    }
  }
}
