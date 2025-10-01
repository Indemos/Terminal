using Core.Common.Models;
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
    Task<IList<InstrumentModel>> Options(MetaModel criteria);

    /// <summary>
    /// Update options
    /// </summary>
    /// <param name="options"></param>
    Task<StatusResponse> Store(List<InstrumentModel> options);
  }

  public class OptionsGrain : Grain<OptionsModel>, IOptionsGrain
  {
    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<IList<InstrumentModel>> Options(MetaModel criteria) => Task.FromResult(State.Options);

    /// <summary>
    /// Update options
    /// </summary>
    /// <param name="options"></param>
    public virtual Task<StatusResponse> Store(List<InstrumentModel> options)
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
