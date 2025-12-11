using System.Collections.Generic;
using System.Linq;

namespace Core.Services
{
  public class AverageService
  {
    /// <summary>
    /// Simple moving average
    /// </summary>
    /// <param name="items"></param>
    /// <param name="index"></param>
    /// <param name="interval"></param>
    public virtual double SimpleAverage(IList<double> items, int index, int interval) => items
      .Skip(index - interval + 1)
      .Take(interval)
      .DefaultIfEmpty(0)
      .Average();

    /// <summary>
    /// Exponential moving average
    /// </summary>
    /// <param name="items"></param>
    /// <param name="index"></param>
    /// <param name="interval"></param>
    /// <param name="previous"></param>
    public virtual double ExponentialAverage(IList<double> items, int index, int interval, double previous)
    {
      var pr = 2.0 / (interval + 1.0);
      var response = items.ElementAtOrDefault(index);

      if (items.Count > index)
      {
        response = response * pr + previous * (1 - pr);
      }

      return response;
    }

    /// <summary>
    /// Linear weighted moving average
    /// </summary>
    /// <param name="items"></param>
    /// <param name="index"></param>
    /// <param name="interval"></param>
    public virtual double LinearWeightAverage(IList<double> items, int index, int interval)
    {
      var sum = 0.0;
      var wsum = 0.0;
      var response = items.ElementAtOrDefault(index);

      if (items.Count > index)
      {
        for (var i = interval; i > 0; i--)
        {
          wsum += i;
          sum += items.ElementAtOrDefault(index - i + 1) * (interval - i + 1);
        }

        response = sum / wsum;
      }

      return response;
    }
  }
}
