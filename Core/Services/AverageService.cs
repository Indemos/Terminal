using System.Collections.Generic;
using System.Linq;

namespace Terminal.Core.Services
{
  public class AverageService
  {
    /// <summary>
    /// Simple moving average
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="index"></param>
    /// <param name="interval"></param>
    /// <returns></returns>
    public virtual decimal SimpleAverage(IList<decimal> collection, int index, int interval)
    {
      var v = 0m;

      if (interval > 0 && index >= interval - 1)
      {
        for (var i = 0; i < interval; i++)
        {
          v += collection.ElementAt(index - i);
        }

        v /= interval;
      }

      return v;
    }

    /// <summary>
    /// Exponential moving average
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="index"></param>
    /// <param name="interval"></param>
    /// <param name="previous"></param>
    /// <returns></returns>
    public virtual decimal ExponentialAverage(IList<decimal> collection, int index, int interval, decimal previous)
    {
      var v = 0m;

      if (interval > 0)
      {
        var pr = 2 / (interval + 1);
        v = collection.ElementAt(index) * pr + previous * (1 - pr);
      }

      return v;
    }

    /// <summary>
    /// Smooth moving average
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="index"></param>
    /// <param name="interval"></param>
    /// <param name="previous"></param>
    /// <returns></returns>
    public virtual decimal SmoothAverage(IList<decimal> collection, int index, int interval, decimal previous)
    {
      var v = 0m;

      if (interval > 0)
      {
        if (index == interval - 1)
        {
          for (var i = 0; i < interval; i++)
          {
            v += collection.ElementAt(index - i);
          }

          v /= interval;
        }

        if (index >= interval)
        {
          v = (previous * (interval - 1) + collection.ElementAt(index)) / interval;
        }
      }

      return v;
    }

    /// <summary>
    /// Linear weighted moving average
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="index"></param>
    /// <param name="interval"></param>
    /// <returns></returns>
    public virtual decimal LinearWeightAverage(IList<decimal> collection, int index, int interval)
    {
      var v = 0m;
      var sum = 0m;
      var wsum = 0m;

      if (interval > 0 && index >= interval - 1)
      {
        for (var i = interval; i > 0; i--)
        {
          wsum += i;
          sum += collection.ElementAt(index - i + 1) * (interval - i + 1);
        }

        v = sum / wsum;
      }

      return v;
    }
  }
}
