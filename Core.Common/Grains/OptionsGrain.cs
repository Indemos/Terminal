using Core.Common.States;
using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface IOptionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Option chain
    /// </summary>
    Task<InstrumentsResponse> Options();

    /// <summary>
    /// Update options
    /// </summary>
    Task StoreOptions(List<InstrumentState> options);
  }

  public class OptionsGrain : Grain<OptionsState>, IOptionsGrain
  {
    /// <summary>
    /// Option chain
    /// </summary>
    public Task<InstrumentsResponse> Options() => Task.FromResult(new InstrumentsResponse
    {
      Data = State.Options
    });

    /// <summary>
    /// Update options
    /// </summary>
    public Task StoreOptions(List<InstrumentState> options)
    {
      if (options is not null)
      {
        State = State with
        {
          Options = options
        };
      }

      return Task.CompletedTask;
    }
  }
}
