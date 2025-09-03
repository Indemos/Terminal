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
  public interface IOrdersGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get orders
    /// </summary>
    Task<OrderState[]> Orders();

    /// <summary>
    /// Add order to the list
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Add(OrderState order);

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Remove(OrderState order);
  }

  public class OrdersGrain : Grain<OrdersState>, IOrdersGrain
  {
    /// <summary>
    /// Descriptor
    /// </summary>
    protected Descriptor descriptor;

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
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<Descriptor>(this.GetPrimaryKeyString());

      orderStream = this
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
    /// Get orders
    /// </summary>
    public async Task<OrderState[]> Orders()
    {
      return await Task.WhenAll(State.Grains.Values.Select(o => o.Order()));
    }

    /// <summary>
    /// Add order to the list
    /// </summary>
    /// <param name="order"></param>
    public Task<DescriptorResponse> Add(OrderState order)
    {
      var orderDescriptor = new OrderDescriptor
      {
        Account = descriptor.Account,
        Order = order.Id
      };

      var response = new DescriptorResponse
      {
        Data = order.Id
      };

      State.Grains[order.Id] = GrainFactory.Get<IOrderGrain>(orderDescriptor);

      return Task.FromResult(response);
    }

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    public Task<DescriptorResponse> Remove(OrderState order)
    {
      var orderGrain = State.Grains.Get(order.Id);
      var response = new DescriptorResponse
      {
        Data = order.Id
      };

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
