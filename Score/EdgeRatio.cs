using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;

namespace ScoreSpace
{
  /// <summary>
  /// Edge ratio or E-Ratio
  /// Ratio between MFE and MAE to identify if trades have bias towards profit
  /// N = Number of observations
  /// AvgEdge = ((MFE / MAE) + (MFE / MAE) + ... + (MFE / MAE)) / N
  /// </summary>
  public class EdgeRatio
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
      var sum = Values.Sum(o =>
      {
        var maxGain = o.Max - o.Value;
        var maxLoss = o.Value - o.Min;

        if (maxLoss == 0)
        {
          return 0.0;
        }

        return maxGain / maxLoss;
      });

      return sum / Values.Count();
    }
  }
}
