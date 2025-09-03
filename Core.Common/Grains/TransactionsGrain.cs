using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface ITransactionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get transactions
    /// </summary>
    Task<OrderState[]> Transactions();
  }

  public class TransactionsGrain : Grain<TransactionsState>, ITransactionsGrain
  {
    /// <summary>
    /// Descriptor
    /// </summary>
    protected Descriptor descriptor;

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
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<Descriptor>(this.GetPrimaryKeyString());

      var orderStream = this
        .GetStreamProvider(nameof(StreamEnum.Order))
        .GetStream<OrderState>(descriptor.Account, Guid.Empty);

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
    /// Get transactions
    /// </summary>
    public async Task<OrderState[]> Transactions()
    {
      return await Task.WhenAll(State.Grains.Select(o => o.Transaction()));
    }

    /// <summary>
    /// Add to the list
    /// </summary>
    /// <param name="order"></param>
    protected async Task Add(OrderState order)
    {
      var orderDescriptor = new OrderDescriptor
      {
        Account = descriptor.Account,
        Order = order.Id
      };

      var orderGrain = GrainFactory.Get<ITransactionGrain>(orderDescriptor);

      await orderGrain.StoreTransaction(order);

      State.Grains.Add(orderGrain);
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
        await Add(order);
      }
    }
  }
}
