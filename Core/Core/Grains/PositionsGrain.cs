using Core.Enums;
using Core.Extensions;
using Core.Models;
using Orleans;
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
    Task<OrdersResponse> Positions(Criteria criteria);

    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(Order order);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Tap(Instrument instrument);

    /// <summary>
    /// Clear positions
    /// </summary>
    Task<StatusResponse> Clear();
  }

  public class PositionsGrain : Grain<Positions>, IPositionsGrain
  {
    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Positions(Criteria criteria)
    {
      var items = await Task.WhenAll(State
        .Grains
        .Values
        .Select(o => o.Position()));

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(Order order)
    {
      var descriptor = this.GetDescriptor();
      var instrument = order.Operation.Instrument.Name;
      var grain = State.Grains.Get(instrument);

      if (grain is null)
      {
        var positionGrain = GrainFactory.GetGrain<IPositionGrain>(this.GetDescriptor(order.Id));
        var orderResponse = await positionGrain.Store(order);

        State.Grains[instrument] = positionGrain;

        return orderResponse;
      }

      var response = await grain.Combine(order);

      if (response.Data is null)
      {
        State.Grains.Remove(instrument);
      }

      if (response.Transaction is not null)
      {
        await GrainFactory
          .GetGrain<ITransactionsGrain>(descriptor)
          .Store(response.Transaction);
      }

      return response;
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="instrument"></param>
    public virtual async Task<StatusResponse> Tap(Instrument instrument)
    {
      foreach (var grain in State.Grains)
      {
        if (Equals(grain.Key, instrument.Name))
        {
          await grain.Value.Tap(instrument);
        }
      }

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
