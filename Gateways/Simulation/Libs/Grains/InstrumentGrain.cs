using Core.Grains;
using Core.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public interface ISimInstrumentGrain : IInstrumentGrain
  {
  }

  public class SimInstrumentGrain : InstrumentGrain, ISimInstrumentGrain
  {
    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> Prices(PriceCriteria criteria)
    {
      var items = State.Items
        .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate?.Ticks)
        .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate?.Ticks)
        .TakeLast(criteria?.Count ?? State.Items.Count)
        .ToArray();

      return Task.FromResult(new PricesResponse
      {
        Data = items
      });
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> PriceGroups(PriceCriteria criteria)
    {
      var items = State.ItemGroups
        .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate?.Ticks)
        .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate?.Ticks)
        .TakeLast(criteria?.Count ?? State.ItemGroups.Count)
        .ToArray();

      return Task.FromResult(new PricesResponse
      {
        Data = items
      });
    }
  }
}
