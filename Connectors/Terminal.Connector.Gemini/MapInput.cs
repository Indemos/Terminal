using Core.ModelSpace;
using System.Collections;
using System.Collections.Generic;

namespace Gateway.Gemini
{
  /// <summary>
  /// Implementation
  /// </summary>
  public class MapInput
  {
    /// <summary>
    /// Convert to external position type
    /// </summary>
    /// <param name="inputs"></param>
    public static IList<ITransactionPositionModel> Positions(dynamic inputs)
    {
      var positions = new List<ITransactionPositionModel>();

      if (inputs is IEnumerable)
      {
        foreach (var input in inputs)
        {
          var position = new TransactionPositionModel
          {
            
          };
        }
      }

      return positions;
    }
  }
}
