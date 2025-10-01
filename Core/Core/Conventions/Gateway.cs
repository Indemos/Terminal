using Core.Enums;
using Core.Extensions;
using Core.Models;
using Core.Validators;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Conventions
{
  public interface IGateway : IDisposable
  {
    /// <summary>
    /// Namespace
    /// </summary>
    string Space { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    AccountModel Account { get; set; }

    /// <summary>
    /// Cluster client
    /// </summary>
    IClusterClient Connector { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    Func<PriceModel, Task> Subscription { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    IAsyncStream<PriceModel> Stream { get; }

    /// <summary>
    /// Order stream
    /// </summary>
    IAsyncStream<OrderModel> OrderStream { get; }

    /// <summary>
    /// Message stream
    /// </summary>
    IAsyncStream<MessageModel> MessageStream { get; }

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
    Task<StatusResponse> Subscribe(InstrumentModel instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Unsubscribe(InstrumentModel instrument);

    /// <summary>
    /// Subscribe
    /// </summary>
    Task<StatusResponse> Subscribe();

    /// <summary>
    /// Unsubscribe
    /// </summary>
    Task<StatusResponse> Unsubscribe();

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    Task<DomModel> Dom(MetaModel criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> Bars(MetaModel criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> Ticks(MetaModel criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<InstrumentModel>> Options(MetaModel criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Positions(MetaModel criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Orders(MetaModel criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Transactions(MetaModel criteria);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    Task<OrderGroupsResponse> SendOrder(OrderModel order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> ClearOrder(OrderModel order);
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public abstract class Gateway : IGateway
  {
    /// <summary>
    /// Order validator
    /// </summary>
    protected OrderValidator orderValidator = new();

    /// <summary>
    /// Data subscription
    /// </summary>
    protected StreamSubscriptionHandle<PriceModel> dataSubscription;

    /// <summary>
    /// Order subscription
    /// </summary>
    protected StreamSubscriptionHandle<OrderModel> orderSubscription;

    /// <summary>
    /// Namespace
    /// </summary>
    public virtual string Space { get; set; } = $"{Guid.NewGuid()}";

    /// <summary>
    /// Account
    /// </summary>
    public virtual AccountModel Account { get; set; }

    /// <summary>
    /// Cluster client
    /// </summary>
    public virtual IClusterClient Connector { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    public virtual Func<PriceModel, Task> Subscription { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    public virtual IAsyncStream<PriceModel> Stream => Connector
      .GetStreamProvider(nameof(StreamEnum.Price))
      .GetStream<PriceModel>(Account.Name, Guid.Empty);

    /// <summary>
    /// Order stream
    /// </summary>
    public virtual IAsyncStream<OrderModel> OrderStream => Connector
      .GetStreamProvider(nameof(StreamEnum.Order))
      .GetStream<OrderModel>(Account.Name, Guid.Empty);

    /// <summary>
    /// Message stream
    /// </summary>
    public virtual IAsyncStream<MessageModel> MessageStream => Connector
      .GetStreamProvider(nameof(StreamEnum.Message))
      .GetStream<MessageModel>(string.Empty, Guid.Empty);

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
    public abstract Task<StatusResponse> Subscribe(InstrumentModel instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    public abstract Task<StatusResponse> Unsubscribe(InstrumentModel instrument);

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<DomModel> Dom(MetaModel criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<PriceModel>> Bars(MetaModel criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<PriceModel>> Ticks(MetaModel criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<InstrumentModel>> Options(MetaModel criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> Positions(MetaModel criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> Transactions(MetaModel criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> Orders(MetaModel criteria);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    public abstract Task<OrderGroupsResponse> SendOrder(OrderModel order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="order"></param>
    public abstract Task<DescriptorResponse> ClearOrder(OrderModel order);

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
    /// Subscribe to price updates
    /// </summary>
    protected virtual async void ConnectPrices()
    {
      dataSubscription = await Stream.SubscribeAsync((o, v) => Subscription(o));
    }

    /// <summary>
    /// Unsubscribe from price updates
    /// </summary>
    protected virtual async void DisconnectPrices()
    {
      if (dataSubscription is not null)
      {
        await dataSubscription.UnsubscribeAsync();
      }
    }

    /// <summary>
    /// Subscribe to order updates
    /// </summary>
    protected virtual async void ConnectOrders()
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
    protected virtual async void DisconnectOrders()
    {
      if (orderSubscription is not null)
      {
        await orderSubscription.UnsubscribeAsync();
      }
    }

    /// <summary>
    /// Convert hierarchy of orders into a plain list
    /// </summary>
    protected virtual List<OrderModel> Compose(OrderModel order)
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
    protected virtual OrderModel Merge(OrderModel group, OrderModel order)
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
    protected virtual List<ErrorModel> GetErrors(OrderModel order)
    {
      var response = new List<ErrorModel>();
      var orders = order.Orders.Append(order);

      foreach (var subOrder in orders)
      {
        var errors = orderValidator
          .Validate(subOrder)
          .Errors
          .Select(error => new ErrorModel { Message = error.ErrorMessage });

        response.AddRange(errors);
      }

      return response;
    }

    /// <summary>
    /// Generate descriptor
    /// </summary>
    /// <param name="instrument"></param>
    protected virtual DescriptorModel Descriptor(string instrument = null) => new()
    {
      Space = Space,
      Account = Account.Name,
      Instrument = instrument
    };

    /// <summary>
    /// Descriptor
    /// </summary>
    protected virtual T Component<T>(string instrument = null) where T : IGrainWithStringKey => Connector.Get<T>(Descriptor(instrument));
  }
}
