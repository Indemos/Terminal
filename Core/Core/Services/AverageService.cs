using System.Collections.Generic;
using System.Linq;

namespace Core.Services
{
  public class AverageService
  {
    /// <summary>
    /// Simple moving average
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="index"></param>
    /// <param name="interval"></param>
    public virtual double SimpleAverage(IEnumerable<double> collection, int index, int interval)
    {
      var response = collection.ElementAtOrDefault(index);

      if (interval > 0 && index >= interval - 1)
      {
        response = collection
          .Skip(index - interval + 1)
          .Take(interval)
          .Average();
      }

      return response;
    }

    /// <summary>
    /// Exponential moving average
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="index"></param>
    /// <param name="interval"></param>
    /// <param name="previous"></param>
    /// <returns></returns>
    public virtual double ExponentialAverage(IEnumerable<double> collection, int index, int interval, double previous)
    {
      var pr = 2.0 / (interval + 1.0);
      var response = collection.ElementAtOrDefault(index);

      if (interval > 0)
      {
        response = collection.ElementAt(index) * pr + previous * (1 - pr);
      }

      return response;
    }

    /// <summary>
    /// Smooth moving average
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="index"></param>
    /// <param name="interval"></param>
    /// <param name="previous"></param>
    /// <returns></returns>
    public virtual double SmoothAverage(IEnumerable<double> collection, int index, int interval, double previous)
    {
      var response = collection.ElementAtOrDefault(index);

      if (interval > 0)
      {
        if (index == interval - 1)
        {
          response = collection
            .Skip(index - interval + 1)
            .Take(interval)
            .Average();
        }

        if (index >= interval)
        {
          response = (previous * (interval - 1) + collection.ElementAt(index)) / interval;
        }
      }

      return response;
    }

    /// <summary>
    /// Linear weighted moving average
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="index"></param>
    /// <param name="interval"></param>
    /// <returns></returns>
    public virtual double LinearWeightAverage(IEnumerable<double> collection, int index, int interval)
    {
      var sum = 0.0;
      var wsum = 0.0;
      var response = collection.ElementAtOrDefault(index);

      if (interval > 0 && index >= interval - 1)
      {
        for (var i = interval; i > 0; i--)
        {
          wsum += i;
          sum += collection.ElementAt(index - i + 1) * (interval - i + 1);
        }

        response = sum / wsum;
      }

      return response;
    }
  }
}
