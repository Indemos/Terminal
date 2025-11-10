using Core.Grains;
using Core.Models;
using System.Collections.Generic;
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
    public override Task<PricesResponse> Prices(Criteria criteria)
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
    public override Task<PricesResponse> PriceGroups(Criteria criteria)
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
