using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using Google.Protobuf.Reflection;
using IBApi;
using IBApi.Messages;
using IBApi.Queries;
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
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);

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

  public class InterConnectionGrain : ConnectionGrain, IInterConnectionGrain
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
    /// Observer
    /// </summary>
    protected ITradeObserver observer;

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
    /// <param name="grainObserver"></param>
    public virtual Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;

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

      connector = new InterBroker
      {
        Port = state.Port,
        Span = state.Span,
        Timeout = state.Timeout
      };

      var id = await connector.Connect();

      if (id is 0)
      {
        await messenger.OnNextAsync(new Message()
        {
          Content = "No connection",
          Action = ActionEnum.Disconnect
        });

        return new()
        {
          Data = StatusEnum.Inactive
        };
      }

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
      var contracts = await connector.GetContracts(contract, cleaner.Token);
      var contractMessage = contracts.FirstOrDefault();

      if (contractMessage is null)
      {
        await messenger.OnNextAsync(new Message()
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
      var dataMessage = new PriceStreamMessage
      {
        DataTypes = [IBApi.Enums.SubscriptionEnum.Price],
        Contract = contract
      };

      subscriptions[instrument.Name] = connector.SubscribeToTicks(dataMessage, async priceMessage =>
      {
        var price = Downstream.MapPrice(priceMessage);
        var group = await instrumentGrain.Send(instrument with { Price = price });

        observer.StreamPrice(group);

        await ordersGrain.Tap(group);
        await positionsGrain.Tap(group);
        await observer.StreamTrade(group);
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
      var query = new HistoricalTicksQuery()
      {
        Contract = contract,
        MinDate = criteria.MinDate.Value,
        MaxDate = criteria.MaxDate.Value,
        DataType = "BID_ASK",
        Count = criteria.Count ?? 1
      };

      var sourceItems = await connector.GetTicks(query, cleaner.Token);
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
      var query = new HistoricalBarsQuery
      {
        Contract = contract,
        MaxDate = maxDate,
        Duration = criteria.Data.Get("Duration"),
        BarType = criteria.Data.Get("BarType"),
        DataType = criteria.Data.Get("DataType"),
      };

      var sourceItems = await connector.GetBars(query, cleaner.Token);
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
      var sourceItems = await connector.GetContracts(contract, cleaner.Token);
      var items = sourceItems.Select(o => Downstream.MapInstrumentType(o.Contract)).ToArray();

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
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Orders(Criteria criteria)
    {
      var descriptor = this.GetDescriptor();
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(descriptor);
      var cleaner = new CancellationTokenSource(state.Timeout);
      var sourceItems = await connector.GetOrders(cleaner.Token);
      var items = sourceItems.Select(Downstream.MapOrder).ToArray();

      await ordersGrain.Clear();
      await Task.WhenAll(items.Select(ordersGrain.Store));
      await Task.Delay(state.Span);

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Positions(Criteria criteria)
    {
      var descriptor = this.GetDescriptor();
      var positionsGrain = GrainFactory.GetGrain<IPositionsGrain>(descriptor);
      var cleaner = new CancellationTokenSource(state.Timeout);
      var sourceItems = await connector.GetPositions(state.Account.Descriptor, cleaner.Token);
      var items = sourceItems.Where(o => o.Position is not 0).Select(Downstream.MapPosition).ToArray();

      await positionsGrain.Clear();
      await Task.WhenAll(items.Select(positionsGrain.Store));
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
      var contract = Upstream.MapContract(order.Operation.Instrument);
      var (orderMessage, SL, TP) = Upstream.MapOrder(order, state.Account);
      var (group, braces) = connector.SendOrder(contract, orderMessage, SL, TP);

      order = order with { Operation = order.Operation with { Id = $"{group.OrderId}" } };

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
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(descriptor);

      connector.ClearOrder(int.Parse(order.Id));

      await ordersGrain.Clear(order);
      await Task.Delay(state.Span);

      return new()
      {
        Data = order.Id
      };
    }
  }
}
