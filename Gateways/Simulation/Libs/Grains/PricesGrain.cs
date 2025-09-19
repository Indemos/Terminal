using Core.Common.Grains;
using Core.Common.States;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public interface ISimPricesGrain : IPricesGrain
  {
  }

  public class SimPricesGrain : PricesGrain, ISimPricesGrain
  {
    /// <summary>
    /// List of prices by criteria
    /// </summary>
    public override Task<IList<PriceState>> Prices(MetaState criteria)
    {
      var prices = State.Prices
          .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate)
          .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate)
          .TakeLast(criteria?.Count ?? State.Prices.Count)
          .ToArray();

      return Task.FromResult<IList<PriceState>>(prices);
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    public override Task<IList<PriceState>> PriceGroups(MetaState criteria)
    {
      var prices = State.PriceGroups
          .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate)
          .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate)
          .TakeLast(criteria?.Count ?? State.PriceGroups.Count)
          .ToArray();

      return Task.FromResult<IList<PriceState>>(prices);
    }
  }
}
