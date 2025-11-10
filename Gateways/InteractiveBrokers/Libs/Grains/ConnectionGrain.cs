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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveBrokers
{
  public interface IInterConnectionGrain : IConnectionGrain
  {
    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(Connection connection);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> PriceGroups(Criteria criteria);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> Prices(Criteria criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> Orders(Criteria criteria);

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> Positions(Criteria criteria);

    /// <summary>
    /// Get contract definitions
    /// </summary>
    /// <param name="criteria"></param>
    Task<InstrumentsResponse> Options(Criteria criteria);

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> SendOrder(Core.Models.Order order);

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> ClearOrder(Core.Models.Order order);

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    Task<Account> AccountSummary();
  }

  public class InterConnectionGrain(MessageService messenger) : ConnectionGrain(messenger), IInterConnectionGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// IB client
    /// </summary>
    protected InterBroker connector;

    /// <summary>
    /// Asset subscriptions
    /// </summary>
    protected ConcurrentDictionary<string, int> subscriptions = new();

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
    public virtual Task<StatusResponse> Setup(Connection connection)
    {
      state = connection;

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<StatusResponse> Connect()
    {
      await Disconnect();

      connector = new InterBroker { Port = state.Port };
      connector.Connect();

      foreach (var instrument in state.Account.Instruments.Values)
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
    public override Task<StatusResponse> Disconnect()
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
    public override async Task<StatusResponse> Subscribe(Instrument instrument)
    {
      await Unsubscribe(instrument);

      var contract = Upstream.MapContract(instrument);
      var cleaner = new CancellationTokenSource(state.Timeout);
      var contracts = await connector.GetContracts(cleaner.Token, contract);
      var contractMessage = contracts.FirstOrDefault();

      if (contractMessage is null)
      {
        await messenger.Send(new Message()
        {
          Content = "No such instrument",
          Action = ActionEnum.Disconnect
        });

        return new()
        {
          Data = StatusEnum.Inactive
        };
      }

      var name = this.GetDescriptor();
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(name);
      var positionsGrain = GrainFactory.GetGrain<IPositionsGrain>(name);
      var instrumentGrain = GrainFactory.GetGrain<IInstrumentGrain>(this.GetDescriptor(instrument.Name));
      var dataMessage = new DataStreamMessage
      {
        DataTypes = [IBApi.Enums.SubscriptionEnum.Price],
        Contract = contract
      };

      subscriptions[instrument.Name] = connector.SubscribeToTicks(dataMessage, async priceMessage =>
      {
        var price = Downstream.MapPrice(priceMessage);
        var group = await instrumentGrain.Send(instrument with { Price = price });

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
    public override Task<StatusResponse> Unsubscribe(Instrument instrument)
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
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<PricesResponse> Prices(Criteria criteria)
    {
      var contract = Upstream.MapContract(criteria.Instrument);
      var cleaner = new CancellationTokenSource(state.Timeout);
      var sourceItems = await connector.GetTicks(
        cleaner.Token,
        contract,
        criteria.MinDate.Value,
        criteria.MaxDate.Value,
        "BID_ASK",
        criteria.Count ?? 1);

      var items = sourceItems.Select(Downstream.MapPrice).ToArray();

      await Task.Delay(state.Span);

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<PricesResponse> PriceGroups(Criteria criteria)
    {
      var cleaner = new CancellationTokenSource(state.Timeout);
      var contract = Upstream.MapContract(criteria.Instrument);
      var maxDate = criteria.MaxDate ?? DateTime.Now;
      var sourceItems = await connector.GetBars(
        cleaner.Token,
        contract,
        maxDate,
        criteria.Data.Get("Duration"),
        criteria.Data.Get("BarType"),
        criteria.Data.Get("DataType"),
        0);

      var items = sourceItems.Select(Downstream.MapPrice).ToArray();

      await Task.Delay(state.Span);

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// List options
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<InstrumentsResponse> Options(Criteria criteria)
    {
      var instrument = criteria.Instrument;
      var minDate = criteria.MinDate?.ToString($"yyyyMMdd-HH:mm:ss");
      var maxDate = (criteria.MaxDate ?? DateTime.Now).ToString($"yyyyMMdd-HH:mm:ss");
      var contract = Upstream.MapContract(criteria.Instrument);
      var cleaner = new CancellationTokenSource(state.Timeout);
      var sourceItems = await connector.GetContracts(cleaner.Token, contract);
      var items = sourceItems.Select(o => Downstream.MapInstrument(o.Contract)).ToArray();

      await Task.Delay(state.Span);

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    public virtual async Task<Account> AccountSummary()
    {
      var account = new Account();
      var cleaner = new CancellationTokenSource(state.Timeout);
      var message = await connector.GetAccountSummary(cleaner.Token);

      account = account with { Balance = double.Parse(message.Get("NetLiquidation")) };

      await Task.Delay(state.Span);

      return account;
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Positions(Criteria criteria)
    {
      var descriptor = this.GetDescriptor();
      var cleaner = new CancellationTokenSource(state.Timeout);
      var sourceItems = await connector.GetPositions(cleaner.Token, state.Account.Name);
      var items = sourceItems.Select(o => Downstream.MapPosition(o, criteria.Instrument)).ToArray();
      var positionsGrain = GrainFactory.GetGrain<IPositionsGrain>(descriptor);

      await positionsGrain.Clear();
      await Task.WhenAll(items.Select(positionsGrain.Store));
      await Task.Delay(state.Span);

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Orders(Criteria criteria)
    {
      var descriptor = this.GetDescriptor();
      var cleaner = new CancellationTokenSource(state.Timeout);
      var sourceItems = await connector.GetOrders(cleaner.Token);
      var items = sourceItems.Select(Downstream.MapOrder).ToArray();
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(descriptor);

      await ordersGrain.Clear();
      await Task.WhenAll(items.Select(ordersGrain.Send));
      await Task.Delay(state.Span);

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> SendOrder(Core.Models.Order order)
    {
      var descriptor = this.GetDescriptor();
      var cleaner = new CancellationTokenSource(state.Timeout);
      var contract = Upstream.MapContract(order.Operation.Instrument);
      var (orderMessage, SL, TP) = Upstream.MapOrder(connector.Id, order, state.Account);
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(descriptor);

      await connector.SendOrder(cleaner.Token, contract, orderMessage, SL, TP);
      await ordersGrain.Send(order);
      await Task.Delay(state.Span);

      return new()
      {
        Data = order
      };
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<DescriptorResponse> ClearOrder(Core.Models.Order order)
    {
      var descriptor = this.GetDescriptor();
      var cleaner = new CancellationTokenSource(state.Timeout);
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(descriptor);

      await connector.ClearOrder(cleaner.Token, int.Parse(order.Id));
      await ordersGrain.Clear(order);
      await Task.Delay(state.Span);

      return new()
      {
        Data = order.Id
      };
    }
  }
}
