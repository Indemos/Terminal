using Core.Models;
using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IOptionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    Task<InstrumentsResponse> Options(Criteria criteria);

    /// <summary>
    /// Update options
    /// </summary>
    /// <param name="options"></param>
    Task<StatusResponse> Store(List<Instrument> options);
  }

  public class OptionsGrain : Grain<Instruments>, IOptionsGrain
  {
    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<InstrumentsResponse> Options(Criteria criteria)
    {
      var response = new InstrumentsResponse
      {
        Data = State.Items
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Update options
    /// </summary>
    /// <param name="options"></param>
    public virtual Task<StatusResponse> Store(List<Instrument> options)
    {
      if (options is not null)
      {
        State = State with
        {
          Items = options
        };
      }

      return Task.FromResult(new StatusResponse
      {
        Data = Enums.StatusEnum.Active
      });
    }
  }
}
