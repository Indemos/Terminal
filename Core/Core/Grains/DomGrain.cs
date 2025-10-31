using Core.Enums;
using Core.Models;
using Orleans;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IDomGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get DOM
    /// </summary>
    /// <param name="criteria"></param>
    Task<DomModel> Dom(CriteriaModel criteria);

    /// <summary>
    /// Update DOM
    /// </summary>
    /// <param name="dom"></param>
    Task<StatusResponse> Store(DomModel dom);
  }

  public class DomGrain : Grain<DomModel>, IDomGrain
  {
    /// <summary>
    /// Get DOM
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<DomModel> Dom(CriteriaModel criteria) => Task.FromResult(State);

    /// <summary>
    /// Update DOM
    /// </summary>
    /// <param name="dom"></param>
    public virtual Task<StatusResponse> Store(DomModel dom)
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
