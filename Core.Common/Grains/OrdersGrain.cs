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
    Task<OrdersResponse> Orders(MetaState criteria);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Send(OrderState order);

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Remove(OrderState order);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    Task<StatusResponse> Tap(PriceState price);
  }

  public class OrdersGrain : Grain<OrdersState>, IOrdersGrain
  {
    /// <summary>
    /// Descriptor
    /// </summary>
    protected DescriptorState descriptor;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<DescriptorState>(this.GetPrimaryKeyString());

      var dataStream = this
        .GetStreamProvider(nameof(StreamEnum.Price))
        .GetStream<PriceState>(descriptor.Account, Guid.Empty);

      await dataStream.SubscribeAsync((o, x) => Tap(o));
      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Orders(MetaState criteria) => new()
    {
      Data = await Task.WhenAll(State.Grains.Values.Select(o => o.Order()))
    };

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
    public virtual async Task<DescriptorResponse> Send(OrderState order)
    {
      var orderGrain = GrainFactory.Get<IOrderGrain>(descriptor with { Order = order.Id });

      State.Grains[order.Id] = orderGrain;

      return await orderGrain.Store(order);
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    public virtual async Task<StatusResponse> Tap(PriceState price)
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

      return new()
      {
        Data = StatusEnum.Active
      };
    }
  }
}
