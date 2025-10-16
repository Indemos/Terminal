using Core.Enums;
using Core.Models;
using Orleans;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IInstrumentGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get instrument
    /// </summary>
    /// <param name="criteria"></param>
    Task<InstrumentModel> Instrument(MetaModel criteria);

    /// <summary>
    /// Store instrument
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Store(InstrumentModel instrument);
  }

  public class InstrumentGrain : Grain<InstrumentModel>, IInstrumentGrain
  {
    /// <summary>
    /// Get instrument
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<InstrumentModel> Instrument(MetaModel criteria) => Task.FromResult(State);

    /// <summary>
    /// Store instrument
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task<StatusResponse> Store(InstrumentModel instrument)
    {
      State = instrument;

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }
  }
}
