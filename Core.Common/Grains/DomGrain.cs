using Core.Common.Enums;
using Core.Common.States;
using Orleans;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface IDomGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get DOM
    /// </summary>
    /// <param name="criteria"></param>
    Task<DomResponse> Dom(MetaState criteria);

    /// <summary>
    /// Update DOM
    /// </summary>
    /// <param name="dom"></param>
    Task<StatusResponse> Store(DomState dom);
  }

  public class DomGrain : Grain<DomState>, IDomGrain
  {
    /// <summary>
    /// Get DOM
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<DomResponse> Dom(MetaState criteria) => Task.FromResult(new DomResponse
    {
      Data = State
    });

    /// <summary>
    /// Update DOM
    /// </summary>
    /// <param name="dom"></param>
    public virtual Task<StatusResponse> Store(DomState dom)
    {
      if (dom is not null)
      {
        State = State with
        {
          Bids = dom.Bids,
          Asks = dom.Asks,
        };
      }

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }
  }
}
