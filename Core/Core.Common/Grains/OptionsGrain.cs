using Core.Common.States;
using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public class OptionsGrain : Grain<OptionsState>, IGrainWithStringKey
  {
    /// <summary>
    /// Option chain
    /// </summary>
    public Task<InstrumentsResponse> Get() => Task.FromResult(new InstrumentsResponse
    {
      Data = State.Options
    });

    /// <summary>
    /// Update options
    /// </summary>
    public Task Store(List<InstrumentState> options)
    {
      State = State with { Options = options };
      return Task.CompletedTask;
    }
  }
}
