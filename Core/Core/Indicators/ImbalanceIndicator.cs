using Core.Extensions;
using Core.Models;
using System.Collections.Generic;

namespace Core.Indicators
{
  public class ImbalanceIndicator
  {
    protected Dictionary<long, Price> map = new();

    public virtual Price Update(long stamp, Price currentPoint)
    {
      var current = map.Get(stamp);
      var bid = currentPoint.BidSize ?? 0;
      var ask = currentPoint.AskSize ?? 0;
      var response = new Price
      {
        Last = (ask - bid) + (current?.Last ?? 0),
        Bar = new()
        {
          Low = bid + (current?.Bar?.Low ?? 0),
          High = ask + (current?.Bar?.High ?? 0)
        }
      };

      map[stamp] = response;

      return response;
    }

    public virtual double Ratio(long stamp)
    {
      var current = map.Get(stamp);
      var bid = current?.Bar?.Low ?? 0;
      var ask = current?.Bar?.High ?? 0;
      var sum = bid + ask;

      return sum is 0 ? 0 : (ask - bid) / sum; // [-1,1] imbalance
    }
  }
}
