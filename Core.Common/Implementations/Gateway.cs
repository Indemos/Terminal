using Core.Common.Enums;
using Core.Common.Services;
using Core.Common.States;
using Core.Common.Validators;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Common.Implementations
{
  public interface IGateway
  {
    /// <summary>
    /// Account
    /// </summary>
    AccountState Account { get; set; }

    /// <summary>
    /// Cluster client
    /// </summary>
    IClusterClient Connector { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    IAsyncStream<PriceState> Stream { get; }

    /// <summary>
    /// Order stream
    /// </summary>
    IAsyncStream<OrderState> OrderStream { get; }

    /// <summary>
    /// Connect
    /// </summary>
    Task<StatusResponse> Connect();

    /// <summary>
    /// Disconnect
    /// </summary>
    Task<StatusResponse> Disconnect();

    /// <summary>
    /// Subscribe
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Subscribe(InstrumentState instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Unsubscribe(InstrumentState instrument);

    /// <summary>
    /// Subscribe
    /// </summary>
    Task<StatusResponse> Subscribe();

    /// <summary>
    /// Unsubscribe
    /// </summary>
    Task<StatusResponse> Unsubscribe();

    /// <summary>
    /// Get account state
    /// </summary>
    Task<AccountResponse> GetAccount();

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    Task<DomResponse> Dom(MetaState criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> GetBars(MetaState criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> GetTicks(MetaState criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    Task<InstrumentsResponse> GetOptions(MetaState criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetPositions(MetaState criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetOrders(MetaState criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetTransactions(MetaState criteria);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    Task<OrderGroupsResponse> SendOrder(OrderState order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> ClearOrder(OrderState order);
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public abstract class Gateway : IGateway
  {
    /// <summary>
    /// Order subscription
    /// </summary>
    protected StreamSubscriptionHandle<OrderState> orderSubscription;

    /// <summary>
    /// Order validator
    /// </summary>
    protected OrderValidator orderValidator = InstanceService<OrderValidator>.Instance;

    /// <summary>
    /// Account
    /// </summary>
    public virtual AccountState Account { get; set; }

    /// <summary>
    /// Cluster client
    /// </summary>
    public virtual IClusterClient Connector { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    public virtual IAsyncStream<PriceState> Stream => Connector
      .GetStreamProvider(nameof(StreamEnum.Price))
      .GetStream<PriceState>(Account.Descriptor, Guid.Empty);

    /// <summary>
    /// Order stream
    /// </summary>
    public virtual IAsyncStream<OrderState> OrderStream => Connector
      .GetStreamProvider(nameof(StreamEnum.Order))
      .GetStream<OrderState>(Account.Descriptor, Guid.Empty);

    /// <summary>
    /// Connect
    /// </summary>
    public abstract Task<StatusResponse> Connect();

    /// <summary>
    /// Disconnect
    /// </summary>
    public abstract Task<StatusResponse> Disconnect();

    /// <summary>
    /// Subscribe
    /// </summary>
    /// <param name="instrument"></param>
    public abstract Task<StatusResponse> Subscribe(InstrumentState instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    public abstract Task<StatusResponse> Unsubscribe(InstrumentState instrument);

    /// <summary>
    /// Get account state
    /// </summary>
    public abstract Task<AccountResponse> GetAccount();

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<DomResponse> Dom(MetaState criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<PricesResponse> GetBars(MetaState criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<PricesResponse> GetTicks(MetaState criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<InstrumentsResponse> GetOptions(MetaState criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<OrdersResponse> GetPositions(MetaState criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<OrdersResponse> GetTransactions(MetaState criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<OrdersResponse> GetOrders(MetaState criteria);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    public abstract Task<OrderGroupsResponse> SendOrder(OrderState order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="order"></param>
    public abstract Task<DescriptorResponse> ClearOrder(OrderState order);

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose() => Disconnect();

    /// <summary>
    /// Subscribe
    /// </summary>
    public virtual async Task<StatusResponse> Subscribe()
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
    public virtual async Task<StatusResponse> Unsubscribe()
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
    /// Subscribe to order updates
    /// </summary>
    protected virtual async Task ConnectOrders()
    {
      orderSubscription = await OrderStream.SubscribeAsync((o, v) =>
      {
        if (o.Operation.Status is OrderStatusEnum.Transaction)
        {
          Account = Account with
          {
            Performance = Account.Performance + o.Balance.Current
          };
        }

        return Task.CompletedTask;
      });
    }

    /// <summary>
    /// Unsubscribe from order updates
    /// </summary>
    protected virtual async Task DisconnectOrders()
    {
      if (orderSubscription is not null)
      {
        await orderSubscription.UnsubscribeAsync();
      }
    }

    /// <summary>
    /// Convert hierarchy of orders into a plain list
    /// </summary>
    protected virtual List<OrderState> Compose(OrderState order)
    {
      var nextOrders = order
        .Orders
        .Where(o => o.Instruction is InstructionEnum.Side or null)
        .Select(o => Merge(o, order))
        .ToList();

      if (order.Amount is not null)
      {
        nextOrders.Add(Merge(order, order));
      }

      return nextOrders;
    }

    /// <summary>
    /// Update side order from group
    /// </summary>
    /// <param name="group"></param>
    /// <param name="order"></param>
    protected virtual OrderState Merge(OrderState group, OrderState order)
    {
      var groupOrders = order
        ?.Orders
        ?.Where(o => o.Instruction is InstructionEnum.Brace)
        ?.Select(o => Merge(group, o));

      var nextOrder = order with
      {
        Descriptor = group.Descriptor,
        Type = order.Type ?? group.Type ?? OrderTypeEnum.Market,
        TimeSpan = order.TimeSpan ?? group.TimeSpan ?? OrderTimeSpanEnum.Gtc,
        Instruction = order.Instruction ?? InstructionEnum.Side,
        Operation = order.Operation with { Time = order?.Operation?.Instrument?.Price?.Time },
        Orders = [.. groupOrders]
      };

      return nextOrder;
    }

    /// <summary>
    /// Preprocess order
    /// </summary>
    /// <param name="order"></param>
    protected virtual List<ErrorState> GetErrors(OrderState order)
    {
      var response = new List<ErrorState>();
      var orders = order.Orders.Append(order);

      foreach (var subOrder in orders)
      {
        var errors = orderValidator
          .Validate(subOrder)
          .Errors
          .Select(error => new ErrorState { Message = error.ErrorMessage });

        response.AddRange(errors);
      }

      return response;
    }
  }
}
