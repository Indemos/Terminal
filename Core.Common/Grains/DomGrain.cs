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
    Task<DomResponse> Dom();

    /// <summary>
    /// Update DOM
    /// </summary>
    Task StoreDom(DomState dom);
  }

  public class DomGrain : Grain<DomState>, IDomGrain
  {
    /// <summary>
    /// Get DOM
    /// </summary>
    public Task<DomResponse> Dom() => Task.FromResult(new DomResponse
    {
      Data = State
    });

    /// <summary>
    /// Update DOM
    /// </summary>
    public Task StoreDom(DomState dom)
    {
      if (dom is not null)
      {
        State = State with
        {
          Bids = dom.Bids,
          Asks = dom.Asks
        };
      }

      return Task.CompletedTask;
    }
  }
}
