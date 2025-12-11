using Core.Conventions;
using Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Indicators
{
  public class ImbalanceIndicator : Indicator
  {
    /// <summary>
    /// Mode
    /// </summary>
    public virtual int Mode { get; set; } = 0;

    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="collection"></param>
    public override Task<IIndicator> Update(IList<Price> collection)
    {
      var response = Task.FromResult<IIndicator>(this);
      var currentPoint = collection.LastOrDefault();

      if (currentPoint is null)
      {
        return response;
      }

      var value = 0.0;

      if (Equals(currentPoint.Bar.Time, Response.Time))
      {
        value = Response.Last.Value;
      }

      switch (Mode)
      {
        case 0: value += currentPoint.AskSize.Value - currentPoint.BidSize.Value; break;
        case 1: value += currentPoint.AskSize.Value; break;
        case -1: value += currentPoint.BidSize.Value; break;
      }

      Response = Response with
      {
        Last = value,
        Time = currentPoint.Bar.Time
      };

      return response;
    }
  }
}
