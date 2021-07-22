using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// Maximum favorable excursion
  /// Maximum unrealized profit
  /// </summary>
  public class MFE
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
      if (Values.Any() == false)
      {
        return 0.0;
      }

      return Values.Average(o => o.Max - o.Value);
    }
  }
}
