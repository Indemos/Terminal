using Core.Common.Grains;
using Core.Common.States;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public interface IPricesGrainAdapter : IPricesGrain
  {
  }

  public class PricesGrainAdapter : PricesGrain, IPricesGrainAdapter
  {
    /// <summary>
    /// List of prices by criteria
    /// </summary>
    public override Task<PricesResponse> Prices(MetaState criteria)
    {
      var response = new PricesResponse
      {
        Data = [.. State.Prices
          .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate)
          .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate)
          .TakeLast(criteria?.Count ?? State.Prices.Count)]
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    public override Task<PricesResponse> PriceGroups(MetaState criteria)
    {
      var response = new PricesResponse
      {
        Data = [.. State.PriceGroups
          .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate)
          .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate)
          .TakeLast(criteria?.Count ?? State.PriceGroups.Count)]
      };

      return Task.FromResult(response);
    }
  }
}
