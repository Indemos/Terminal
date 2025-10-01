using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IGatewayOptionsGrain : IOptionsGrain
  {
  }

  public class GatewayOptionsGrain : OptionsGrain, IGatewayOptionsGrain
  {
    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<IList<InstrumentModel>> Options(MetaModel criteria)
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

      return Task.FromResult<IList<InstrumentModel>>(options);
    }
  }
}
