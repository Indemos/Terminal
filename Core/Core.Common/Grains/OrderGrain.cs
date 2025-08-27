using Core.Common.Enums;
using Core.Common.States;
using Orleans;
using Orleans.Streams;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public class OrderGrain : Grain<OrderState>, IGrainWithStringKey
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
      var descriptor = this.GetPrimaryKeyString();
      var dataStream = this
        .GetStreamProvider(nameof(StreamEnum.Price))
        .GetStream<PriceState>(descriptor, Guid.Empty);

      orderStream = this
        .GetStreamProvider(nameof(StreamEnum.Order))
        .GetStream<OrderState>(descriptor, Guid.Empty);

      dataSubscription = await dataStream.SubscribeAsync(OnPoint);

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
    public Task<OrderState> Get()
    {
      return Task.FromResult(State);
    }

    /// <summary>
    /// Get order state
    /// </summary>
    public async Task Send(OrderState order)
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
    protected async Task OnPoint(PriceState point, StreamSequenceToken token)
    {
      if (Equals(point.Instrument.Name, State.Operation.Instrument.Name))
      {
        State = State with
        {
          Operation = State.Operation with
          {
            Instrument = point.Instrument
          }
        };

        if (IsExecutable())
        {
          State = State with
          {
            Operation = State.Operation with
            {
              Status = OrderStatusEnum.Position
            }
          };

          await orderStream.OnNextAsync(State);
        }
      }
    }
  }
}
