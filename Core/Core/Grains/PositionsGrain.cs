using Core.Enums;
using Core.Extensions;
using Core.Models;
using Orleans;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IPositionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Positions(MetaModel criteria);

    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(OrderModel order);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    Task<StatusResponse> Tap(PriceModel price);

    /// <summary>
    /// Clear positions
    /// </summary>
    Task<StatusResponse> Clear();
  }

  public class PositionsGrain : Grain<PositionsModel>, IPositionsGrain
  {
    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderModel>> Positions(MetaModel criteria) => await Task.WhenAll(State
      .Grains
      .Values
      .Select(o => o.Position()));

    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(OrderModel order)
    {
      var name = this.GetPrimaryKeyString();
      var instrument = order.Operation.Instrument.Name;
      var grain = State.Grains.Get(instrument);

      if (grain is null)
      {
        grain = State.Grains[instrument] = GrainFactory.GetGrain<IPositionGrain>($"{name}:{order.Id}");
        return await grain.Store(order);
      }

      var response = await grain.Combine(order);

      if (response.Data is null)
      {
        State.Grains.Remove(instrument);
      }

      if (response.Transaction is not null)
      {
        await GrainFactory
          .GetGrain<ITransactionsGrain>(name)
          .Store(response.Transaction);
      }

      return response;
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    public virtual async Task<StatusResponse> Tap(PriceModel price)
    {
      await Task.WhenAll(State.Grains.Values.Select(o => o.Tap(price)));

      return new StatusResponse
      {
        Data = Enums.StatusEnum.Active
      };
    }

    /// <summary>
    /// Clear positions
    /// </summary>
    public virtual Task<StatusResponse> Clear()
    {
      State.Grains.Clear();

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Inactive
      });
    }
  }
}
