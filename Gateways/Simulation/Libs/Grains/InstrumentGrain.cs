using Core.Grains;
using Core.Models;
using System;
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
    /// <param name="criteria"></param>
    public override Task<PricesResponse> Prices(PriceCriteria criteria)
    {
      var items = State.Items;
      var count = Math.Min(criteria?.Count ?? items.Count, items.Count);
      var response = new List<Price>(count);
      var minTime = criteria?.MinDate?.Ticks;
      var maxTime = criteria?.MaxDate?.Ticks;

      for (var i = items.Count - 1; i >= 0 && response.Count < count; i--)
      {
        var item = items[i];

        if (minTime.HasValue && item.Time < minTime.Value) break;
        if (maxTime.HasValue && item.Time > maxTime.Value) continue;

        response.Add(item);
      }

      response.Reverse();

      return Task.FromResult(new PricesResponse
      {
        Data = response
      });
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<PricesResponse> PriceGroups(PriceCriteria criteria)
    {
      var items = State.ItemGroups;
      var count = Math.Min(criteria?.Count ?? items.Count, items.Count);
      var response = new List<Price>(count);
      var minTime = criteria?.MinDate?.Ticks;
      var maxTime = criteria?.MaxDate?.Ticks;

      for (var i = items.Count - 1; i >= 0 && response.Count < count; i--)
      {
        var item = items[i];

        if (minTime.HasValue && item.Time < minTime.Value) break;
        if (maxTime.HasValue && item.Time > maxTime.Value) continue;

        response.Add(item);
      }

      response.Reverse();

      return Task.FromResult(new PricesResponse
      {
        Data = response
      });
    }
  }
}
