using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// GHPR or geometric holding period returns
  /// I = Initial balance
  /// O = Final balance
  /// HPR = Holding period returns for one deal = O / I
  /// N = Number of deals 
  /// GHPR = (HPR[0] + HPR[1] + ... + HPR[N]) ^ (1 / N)
  /// </summary>
  public class GHPR
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
      var sum = 1.0;
      var count = Values.Count();
      var input = Values.FirstOrDefault();
      var output = Values.LastOrDefault();

      if (count == 0 || input == null || output == null || input.Value == 0)
      {
        return 0.0;
      }

      for (var i = 1; i < count; i++)
      {
        var currentValue = Values.ElementAtOrDefault(i)?.Value ?? 0.0;
        var previousValue = Values.ElementAtOrDefault(i - 1)?.Value ?? 0.0;

        sum *= currentValue / previousValue;
      }

      return Math.Pow(sum, 1.0 / count);
    }
  }
}
