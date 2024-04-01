using System.Collections.Generic;

namespace Terminal.Core.Models
{
  public struct ResponseMapModel<T>
  {
    /// <summary>
    /// Errors count
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Items
    /// </summary>
    public IList<ResponseItemModel<T>> Items { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ResponseMapModel() => Items = new List<ResponseItemModel<T>>();
  }
}
