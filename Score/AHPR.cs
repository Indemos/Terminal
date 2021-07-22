using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// AHPR or average holding period returns
  /// I = Balance before deal
  /// O = Balance after deal
  /// HPR = Holding period returns for one deal = O / I
  /// N = Number of deals 
  /// AHPR = (HPR[0] + HPR[1] + ... + HPR[N]) / N
  /// </summary>
  public class AHPR
  {
    /// <summary>
    /// Input values
    /// </summary>
    public virtual IEnumerable<InputData> Values { get; set; } = new List<InputData>();

    /// <summary>
    /// Calculate
    /// </summary>
    /// <returns></returns>
    public virtual double Calculate()
    {
      var sum = 0.0;
      var count = Values.Count();

      if (count == 0)
      {
        return 0.0;
      }

      for (var i = 1; i < count; i++)
      {
        var currentValue = Values.ElementAtOrDefault(i)?.Value ?? 0.0;
        var previousValue = Values.ElementAtOrDefault(i - 1)?.Value ?? 0.0;

        sum += currentValue / previousValue;
      }

      return sum / count;
    }
  }
}
