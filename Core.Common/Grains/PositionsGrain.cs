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

    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    Task Send(OrderState order);
  }

  public class PositionsGrain : Grain<PositionsState>, IPositionsGrain
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
    /// Get positions
    /// </summary>
    public async Task<OrderState[]> Positions()
    {
      return await Task.WhenAll(State.Grains.Values.Select(o => o.Position()));
    }

    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    public async Task Send(OrderState order)
    {
      if (State.Grains.ContainsKey(order.Operation.Instrument.Name))
      {
        await Combine(order);
        return;
      }

      await Store(order);
    }

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="order"></param>
    protected async Task Combine(OrderState order)
    {
      var name = order.Operation.Instrument.Name;
      var currentGrain = State.Grains.Get(name);
      var response = await currentGrain.Combine(order);

      if (response.Data is not null)
      {
        if (order.Amount.Is(response.Data.Amount))
        {
          State.Grains.Remove(name);
        }

        await GrainFactory
          .Get<ITransactionsGrain>(descriptor)
          .Send(response.Data);
      }
    }

    /// <summary>
    /// Create
    /// </summary>
    /// <param name="order"></param>
    protected async Task Store(OrderState order)
    {
      var nextGrain = GrainFactory.Get<IPositionGrain>(new OrderDescriptor
      {
        Account = descriptor.Account,
        Order = order.Id
      });

      await nextGrain.Store(order);

      State.Grains[order.Operation.Instrument.Name] = nextGrain;
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    /// <param name="token"></param>
    protected async Task OnPrice(PriceState price, StreamSequenceToken token)
    {
      foreach (var grain in State.Grains.Values)
      {
        await grain.Tap(price);
      }
    }
  }
}
