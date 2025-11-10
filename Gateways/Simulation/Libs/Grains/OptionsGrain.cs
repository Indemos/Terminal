using Core.Grains;
using Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public interface ISimOptionsGrain : IOptionsGrain
  {
  }

  public class SimOptionsGrain : OptionsGrain, ISimOptionsGrain
  {
    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<InstrumentsResponse> Options(Criteria criteria)
    {
      var side = criteria?.Instrument?.Derivative?.Side;
      var options = State
        .Items
        .Where(o => side is null || Equals(o.Derivative.Side, side))
        .Where(o => criteria?.MinDate is null || o.Derivative.ExpirationDate?.Date >= criteria?.MinDate?.Date)
        .Where(o => criteria?.MaxDate is null || o.Derivative.ExpirationDate?.Date <= criteria?.MaxDate?.Date)
        .Where(o => criteria?.MinPrice is null || o.Derivative.Strike >= criteria.MinPrice)
        .Where(o => criteria?.MaxPrice is null || o.Derivative.Strike <= criteria.MaxPrice)
        .OrderBy(o => o.Derivative.ExpirationDate)
        .ThenBy(o => o.Derivative.Strike)
        .ThenBy(o => o.Derivative.Side)
        .ToArray();

      return Task.FromResult(new InstrumentsResponse
      {
        Data = options
      });
    }
  }
}
