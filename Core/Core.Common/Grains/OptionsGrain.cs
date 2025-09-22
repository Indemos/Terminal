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
    Task<IList<InstrumentState>> Options(MetaState criteria);

    /// <summary>
    /// Update options
    /// </summary>
    /// <param name="options"></param>
    Task<StatusResponse> Store(List<InstrumentState> options);
  }

  public class OptionsGrain : Grain<OptionsState>, IOptionsGrain
  {
    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<IList<InstrumentState>> Options(MetaState criteria) => Task.FromResult(State.Options);

    /// <summary>
    /// Update options
    /// </summary>
    /// <param name="options"></param>
    public virtual Task<StatusResponse> Store(List<InstrumentState> options)
    {
      if (options is not null)
      {
        State = State with
        {
          Options = options
        };
      }

      return Task.FromResult(new StatusResponse
      {
        Data = Enums.StatusEnum.Active
      });
    }
  }
}
