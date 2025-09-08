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
    /// <param name="criteria"></param>
    Task<OrderState[]> Orders(MetaState criteria);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="order"></param>
    Task Send(OrderState order);

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
    /// Data subscription
    /// </summary>
    protected StreamSubscriptionHandle<PriceState> dataSubscription;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<Descriptor>(this.GetPrimaryKeyString());

      var dataStream = this
        .GetStreamProvider(nameof(StreamEnum.Price))
        .GetStream<PriceState>(descriptor.Account, Guid.Empty);

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
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrderState[]> Orders(MetaState criteria)
    {
      return await Task.WhenAll(State.Grains.Values.Select(o => o.Order()));
    }

    /// <summary>
    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<DescriptorResponse> Remove(OrderState order)
    {
      if (State.Grains.ContainsKey(order.Id))
      {
        State.Grains.Remove(order.Id);
      }

      return Task.FromResult(new DescriptorResponse
      {
        Data = order.Id
      });
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task Send(OrderState order)
    {
      var orderGrain = GrainFactory.Get<IOrderGrain>(new OrderDescriptor
      {
        Account = descriptor.Account,
        Order = order.Id
      });

      await orderGrain.Store(order);

      State.Grains[order.Id] = orderGrain;
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    /// <param name="token"></param>
    protected virtual async Task OnPrice(PriceState price, StreamSequenceToken token)
    {
      var positionsGrain = GrainFactory.Get<IPositionsGrain>(descriptor);
      var grains = State.Grains.ToArray();

      foreach (var grain in grains)
      {
        if (await grain.Value.IsExecutable(price))
        {
          State.Grains.Remove(grain.Key);
          await positionsGrain.Send(await grain.Value.Position(price));
        }
      }
    }
  }
}
