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
  public interface IPositionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get positions
    /// </summary>
    Task<OrderState[]> Positions();
  }

  public class PositionsGrain : Grain<PositionsState>, IPositionsGrain
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
    /// Get positions
    /// </summary>
    public async Task<OrderState[]> Positions()
    {
      return await Task.WhenAll(State.Grains.Values.Select(o => o.Position()));
    }

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="order"></param>
    protected async Task Update(OrderState order)
    {
      var name = order.Operation.Instrument.Name;
      var currentGrain = State.Grains.Get(name);
      var response = await currentGrain.Combine(order);

      if (response.Data is null)
      {
        State.Grains.Remove(name);
      }
    }

    /// <summary>
    /// Create
    /// </summary>
    /// <param name="order"></param>
    protected async Task Create(OrderState order)
    {
      var name = order.Operation.Instrument.Name;
      var orderDescriptor = new OrderDescriptor
      {
        Account = descriptor.Account,
        Order = order.Id
      };

      var nextGrain = State.Grains[name] = GrainFactory.Get<IPositionGrain>(orderDescriptor);

      await nextGrain.StorePosition(order);
    }

    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    /// <param name="token"></param>
    protected async Task OnOrder(OrderState order, StreamSequenceToken token)
    {
      if (order.Operation.Status is OrderStatusEnum.Position)
      {
        if (State.Grains.Get(order.Operation.Instrument.Name) is null)
        {
          await Create(order);
          return;
        }

        await Update(order);
      }
    }
  }
}
