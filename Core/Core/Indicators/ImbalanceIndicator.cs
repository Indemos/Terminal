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
        BidSize = bid + (current?.BidSize ?? 0),
        AskSize = ask + (current?.AskSize ?? 0)
      };

      map[stamp] = response;

      return response;
    }

    public virtual double Ratio(long stamp)
    {
      var current = map.Get(stamp);
      var bid = current?.BidSize ?? 0;
      var ask = current?.AskSize ?? 0;
      var sum = bid + ask;

      return sum is 0 ? 0 : (ask - bid) / sum; // [-1,1] imbalance
    }
  }
}
