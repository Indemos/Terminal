using Core.Grains;
using Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public interface IGatewayPricesGrain : IPricesGrain
  {
  }

  public class GatewayPricesGrain : PricesGrain, IGatewayPricesGrain
  {
    /// <summary>
    /// List of prices by criteria
    /// </summary>
    public override Task<IList<PriceModel>> Prices(MetaModel criteria)
    {
      var prices = State.Prices
        .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate?.Ticks)
        .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate?.Ticks)
        .TakeLast(criteria?.Count ?? State.Prices.Count)
        .ToArray();

      return Task.FromResult<IList<PriceModel>>(prices);
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    public override Task<IList<PriceModel>> PriceGroups(MetaModel criteria)
    {
      var prices = State.PriceGroups
        .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate?.Ticks)
        .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate?.Ticks)
        .TakeLast(criteria?.Count ?? State.PriceGroups.Count)
        .ToArray();

      return Task.FromResult<IList<PriceModel>>(prices);
    }
  }
}
