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
  public class PositionsGrain : Grain<PositionsState>, IGrainWithStringKey
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
    /// Get position by name
    /// </summary>
    /// <param name="name"></param>
    public Task<PositionGrain> Get(string name)
    {
      return Task.FromResult(State.Grains.Get(name));
    }

    /// <summary>
    /// Get position by index
    /// </summary>
    /// <param name="index"></param>
    public Task<PositionGrain> Get(int index)
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
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    /// <param name="token"></param>
    protected async Task OnOrder(OrderState order, StreamSequenceToken token)
    {
      if (order.Operation.Status is OrderStatusEnum.Position)
      {
        var name = order.Operation.Instrument.Name;
        var currentGrain = State.Grains.Get(name);

        if (currentGrain is not null)
        {
          var response = await currentGrain.Combine(order);

          if (response.Data is null)
          {
            State.Grains.Remove(name);
          }

          return;
        }

        var descriptor = this.GetPrimaryKeyString();
        var nextGrain = GrainFactory.GetGrain<PositionGrain>($"{descriptor}:{order.Id}");

        await nextGrain.Send(order);

        State.Grains[name] = nextGrain;
      }
    }
  }
}
