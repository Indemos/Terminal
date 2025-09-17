using Core.Common.States;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Common.Grains
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
    public override Task<InstrumentsResponse> Options(MetaState criteria)
    {
      var minDate = criteria?.MinDate is long mnd ? new DateTime(mnd) : null as DateTime?;
      var maxDate = criteria?.MaxDate is long mxd ? new DateTime(mxd) : null as DateTime?;
      var side = criteria?.Instrument?.Derivative?.Side;
      var options = State
        .Options
        .Where(o => side is null || Equals(o.Derivative.Side, side))
        .Where(o => minDate is null || o.Derivative.ExpirationDate?.Date >= minDate?.Date)
        .Where(o => maxDate is null || o.Derivative.ExpirationDate?.Date <= maxDate?.Date)
        .Where(o => criteria?.MinPrice is null || o.Derivative.Strike >= criteria.MinPrice)
        .Where(o => criteria?.MaxPrice is null || o.Derivative.Strike <= criteria.MaxPrice)
        .OrderBy(o => o.Derivative.ExpirationDate)
        .ThenBy(o => o.Derivative.Strike)
        .ThenBy(o => o.Derivative.Side)
        .ToArray();

      var response = new InstrumentsResponse
      {
        Data = options
      };

      return Task.FromResult(response);
    }
  }
}
