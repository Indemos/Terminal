using Core.Common.Enums;
using Core.Common.States;
using Orleans;
using Orleans.Streams;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public class TransactionsGrain : Grain<TransactionsState>, IGrainWithStringKey
  {
    /// <summary>
    /// Data subscription
    /// </summary>
    protected StreamSubscriptionHandle<OrderState> orderSubscription;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      var orderStream = this
        .GetStreamProvider(nameof(StreamEnum.Order))
        .GetStream<OrderState>(this.GetPrimaryKeyString(), Guid.Empty);

      orderSubscription = await orderStream.SubscribeAsync(OnOrder);

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Deactivation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellation)
    {
      if (orderSubscription is not null)
      {
        await orderSubscription.UnsubscribeAsync();
      }

      await base.OnDeactivateAsync(reason, cancellation);
    }

    /// <summary>
    /// Get deal by index
    /// </summary>
    /// <param name="index"></param>
    public Task<TransactionGrain> Get(int index)
    {
      return Task.FromResult(State.Grains.ElementAtOrDefault(index));
    }

    /// <summary>
    /// Get count
    /// </summary>
    public Task<int> Count()
    {
      return Task.FromResult(State.Grains.Count);
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="order"></param>
    /// <param name="token"></param>
    protected Task OnOrder(OrderState order, StreamSequenceToken token)
    {
      if (order.Operation.Status is OrderStatusEnum.Transaction)
      {
        var descriptor = this.GetPrimaryKeyString();
        var orderGrain = GrainFactory.GetGrain<TransactionGrain>($"{descriptor}:{order.Id}");

        State.Grains.Add(orderGrain);
      }

      return Task.CompletedTask;
    }
  }
}
