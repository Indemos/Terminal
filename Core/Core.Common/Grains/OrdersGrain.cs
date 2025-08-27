using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.States;
using Orleans;
using Orleans.Streams;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public class OrdersGrain : Grain<OrdersState>, IGrainWithStringKey
  {
    /// <summary>
    /// Order stream
    /// </summary>
    protected IAsyncStream<OrderState> orderStream;

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
      var descriptor = this.GetPrimaryKeyString();

      orderStream = this
        .GetStreamProvider(nameof(StreamEnum.Order))
        .GetStream<OrderState>(descriptor, Guid.Empty);

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
    /// Get order by name
    /// </summary>
    /// <param name="name"></param>
    public Task<OrderGrain> Get(string name)
    {
      return Task.FromResult(State.Grains.Get(name));
    }

    /// <summary>
    /// Get order by index
    /// </summary>
    /// <param name="index"></param>
    public Task<OrderGrain> Get(int index)
    {
      return Task.FromResult(State.Grains.Values.ElementAtOrDefault(index));
    }

    /// <summary>
    /// Get count
    /// </summary>
    public Task<int> Count()
    {
      return Task.FromResult(State.Grains.Count);
    }

    /// <summary>
    /// Add order to the list
    /// </summary>
    /// <param name="order"></param>
    public Task<DescriptorResponse> Add(OrderState order)
    {
      var descriptor = this.GetPrimaryKeyString();
      var response = new DescriptorResponse { Data = order.Id };

      State.Grains[order.Id] = GrainFactory.GetGrain<OrderGrain>($"{descriptor}:{order.Id}");

      return Task.FromResult(response);
    }

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    public Task<DescriptorResponse> Remove(OrderState order)
    {
      var orderGrain = State.Grains.Get(order.Id);
      var response = new DescriptorResponse { Data = order.Id };

      if (orderGrain is not null)
      {
        State.Grains.Remove(order.Id, out _);
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="order"></param>
    /// <param name="token"></param>
    protected async Task OnOrder(OrderState order, StreamSequenceToken token)
    {
      if (order.Operation.Status is OrderStatusEnum.Order)
      {
        await Add(order);
        return;
      }

      await Remove(order);
    }
  }
}
