using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using Orleans.Streams;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface ITransactionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get count
    /// </summary>
    Task<int> Count();

    /// <summary>
    /// Get transaction by index
    /// </summary>
    /// <param name="index"></param>
    Task<ITransactionGrain> Grain(int index);
  }

  public class TransactionsGrain : Grain<TransactionsState>, ITransactionsGrain
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
      var converter = InstanceService<ConversionService>.Instance;
      var baseDescriptor = converter.Decompose<BaseDescriptor>(this.GetPrimaryKeyString());

      var orderStream = this
        .GetStreamProvider(nameof(StreamEnum.Order))
        .GetStream<OrderState>(baseDescriptor.Account, Guid.Empty);

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
    /// Get transaction by index
    /// </summary>
    /// <param name="index"></param>
    public Task<ITransactionGrain> Grain(int index)
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
    protected async Task OnOrder(OrderState order, StreamSequenceToken token)
    {
      if (order.Operation.Status is OrderStatusEnum.Transaction)
      {
        var converter = InstanceService<ConversionService>.Instance;
        var baseDescriptor = converter.Decompose<BaseDescriptor>(this.GetPrimaryKeyString());
        var orderDescriptor = new IdentityDescriptor
        {
          Account = baseDescriptor.Account,
          Identity = order.Id
        };

        var orderGrain = GrainFactory.Get<ITransactionGrain>(orderDescriptor);

        await orderGrain.StoreTransaction(order);

        State.Grains.Add(orderGrain);
      }
    }
  }
}
