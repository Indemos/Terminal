using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// Maximum favorable excursion
  /// Maximum unrealized profit
  /// </summary>
  public class Change
  {
    /// <summary>
    /// Input values
    /// </summary>
    public virtual IEnumerable<InputData> Values { get; set; } = new List<InputData>();

    /// <summary>
    /// Calculate
    /// </summary>
    /// <param name="option"></param>
    /// <returns></returns>
    public virtual double Calculate(int? option = 0)
    {
      var input = Values.FirstOrDefault();
      var output = Values.LastOrDefault();

      if (input == null || output == null || input.Value == 0)
      {
        return 0.0;
      }

      switch (option)
      {
        case 0: return output.Value - input.Value;
        case 1: return (output.Value - input.Value) / Math.Abs(input.Value) * 100;
      }

      return 0.0;
    }
  }
}
