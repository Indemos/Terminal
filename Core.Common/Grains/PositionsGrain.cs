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
    /// <param name="criteria"></param>
    Task<OrdersResponse> Positions(MetaState criteria);

    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Send(OrderState order);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    Task<StatusResponse> Tap(PriceState price);
  }

  public class PositionsGrain : Grain<PositionsState>, IPositionsGrain
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
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    public virtual async Task<StatusResponse> Tap(PriceState price)
    {
      foreach (var grain in State.Grains.Values)
      {
        await grain.Tap(price);
      }

      return new StatusResponse
      {
        Data = Enums.StatusEnum.Active
      };
    }

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Positions(MetaState criteria) => new OrdersResponse
    {
      Data = await Task.WhenAll(State.Grains.Values.Select(o => o.Position()))
    };

    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<DescriptorResponse> Send(OrderState order)
    {
      var response = new DescriptorResponse
      {
        Data = order.Id
      };

      if (State.Grains.ContainsKey(order.Operation.Instrument.Name))
      {
        await Combine(order);
        return response;
      }

      await Store(order);
      return response;
    }

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="order"></param>
    protected virtual async Task Combine(OrderState order)
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
          .Store(response.Data);
      }
    }

    /// <summary>
    /// Create
    /// </summary>
    /// <param name="order"></param>
    protected virtual async Task Store(OrderState order)
    {
      var nextGrain = GrainFactory.Get<IPositionGrain>(descriptor with { Order = order.Id });

      await nextGrain.Store(order);

      State.Grains[order.Operation.Instrument.Name] = nextGrain;
    }
  }
}
