using Core.Common.Enums;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using Orleans.Streams;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface IOrderGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get order state
    /// </summary>
    Task<OrderState> Order();

    /// <summary>
    /// Send order
    /// </summary>
    Task StoreOrder(OrderState order);
  }

  public class OrderGrain : Grain<OrderState>, IOrderGrain
  {
    /// <summary>
    /// Order stream
    /// </summary>
    protected IAsyncStream<OrderState> orderStream;

    /// <summary>
    /// Data subscription
    /// </summary>
    protected StreamSubscriptionHandle<PriceState> dataSubscription;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      var descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<Descriptor>(this.GetPrimaryKeyString());

      var dataStream = this
        .GetStreamProvider(nameof(StreamEnum.Price))
        .GetStream<PriceState>(descriptor.Account, Guid.Empty);

      orderStream = this
        .GetStreamProvider(nameof(StreamEnum.Order))
        .GetStream<OrderState>(descriptor.Account, Guid.Empty);

      dataSubscription = await dataStream.SubscribeAsync(OnPrice);

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Deactivation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellation)
    {
      if (dataSubscription is not null)
      {
        await dataSubscription.UnsubscribeAsync();
      }

      await base.OnDeactivateAsync(reason, cancellation);
    }

    /// <summary>
    /// Get order state
    /// </summary>
    public Task<OrderState> Order()
    {
      return Task.FromResult(State);
    }

    /// <summary>
    /// Send order
    /// </summary>
    public async Task StoreOrder(OrderState order)
    {
      State = order with
      {
        Operation = order.Operation with
        {
          Status = OrderStatusEnum.Order
        }
      };

      await orderStream.OnNextAsync(State);
    }

    /// <summary>
    /// Check if pending order can be executed
    /// </summary>
    protected bool IsExecutable()
    {
      var point = State.Operation.Instrument.Point;
      var isLong = State.Side is OrderSideEnum.Long;
      var isShort = State.Side is OrderSideEnum.Short;

      if (State.Type is OrderTypeEnum.StopLimit)
      {
        var isLongLimit = isLong && point.Ask >= State.ActivationPrice;
        var isShortLimit = isShort && point.Bid <= State.ActivationPrice;

        if (isLongLimit || isShortLimit)
        {
          State = State with { Type = OrderTypeEnum.Limit };
        }
      }

      switch (State.Type)
      {
        case OrderTypeEnum.Market: return true;
        case OrderTypeEnum.Stop: return isLong ? point.Ask >= State.Price : point.Bid <= State.Price;
        case OrderTypeEnum.Limit: return isLong ? point.Ask <= State.Price : point.Bid >= State.Price;
      }

      return false;
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="point"></param>
    /// <param name="token"></param>
    protected async Task OnPrice(PriceState point, StreamSequenceToken token)
    {
      var isConvertible = IsExecutable();
      var isOrder = State.Operation.Status is OrderStatusEnum.Order;
      var isInstrument = Equals(point.Name, State.Operation.Instrument.Name);

      if (isOrder && isInstrument && isConvertible)
      {
        State = State with
        {
          Price = point.Last,
          Operation = State.Operation with
          {
            Time = point.Time,
            Amount = State.Amount,
            AveragePrice = point.Last,
            Status = OrderStatusEnum.Position
          }
        };

        await orderStream.OnNextAsync(State);
      }
    }
  }
}
