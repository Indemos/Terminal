using Core.Common.States;
using Orleans;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public class DomGrain : Grain<DomState>, IGrainWithStringKey
  {
    /// <summary>
    /// Get DOM
    /// </summary>
    public Task<DomResponse> Get() => Task.FromResult(new DomResponse
    {
      Data = State
    });

    /// <summary>
    /// Update DOM
    /// </summary>
    public Task Store(DomState dom)
    {
      State = State with
      {
        Bids = dom.Bids,
        Asks = dom.Asks
      };

      return Task.CompletedTask;
    }
  }
}
