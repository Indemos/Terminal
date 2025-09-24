using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using System.Collections.Generic;
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
    Task<IList<OrderState>> Positions(MetaState criteria);

    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(OrderState order);

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
    /// Transactions
    /// </summary>
    protected ITransactionsGrain actions;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<DescriptorState>(this.GetPrimaryKeyString());

      actions = GrainFactory.Get<ITransactionsGrain>(descriptor);

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderState>> Positions(MetaState criteria) => await Task.WhenAll(State
      .Grains
      .Values
      .Select(o => o.Position()));

    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(OrderState order)
    {
      var name = order.Operation.Instrument.Name;
      var grain = State.Grains.Get(name);

      if (grain is null)
      {
        grain = State.Grains[name] = GrainFactory.Get<IPositionGrain>(descriptor with
        {
          Order = order.Id
        });

        return await grain.Store(order);
      }

      var response = await grain.Combine(order);

      if (response.Data is null)
      {
        State.Grains.Remove(name);
      }

      if (response.Transaction is not null)
      {
        await actions.Store(response.Transaction);
      }

      return response;
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    public virtual async Task<StatusResponse> Tap(PriceState price)
    {
      await Task.WhenAll(State.Grains.Values.Select(o => o.Tap(price)));

      return new StatusResponse
      {
        Data = Enums.StatusEnum.Active
      };
    }
  }
}
