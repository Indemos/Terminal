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
    /// <param name="criteria"></param>
    Task<InstrumentsResponse> Options(MetaState criteria);

    /// <summary>
    /// Update options
    /// </summary>
    /// <param name="options"></param>
    Task Store(List<InstrumentState> options);
  }

  public class OptionsGrain : Grain<OptionsState>, IOptionsGrain
  {
    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<InstrumentsResponse> Options(MetaState criteria) => Task.FromResult(new InstrumentsResponse
    {
      Data = State.Options
    });

    /// <summary>
    /// Update options
    /// </summary>
    /// <param name="options"></param>
    public virtual Task Store(List<InstrumentState> options)
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
