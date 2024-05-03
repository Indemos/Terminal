using System.Collections.Generic;

namespace Terminal.Core.Models
{
  public class ResponseMapModel<T>
  {
    /// <summary>
    /// Errors count
    /// </summary>
    public virtual int Count { get; set; }

    /// <summary>
    /// Items
    /// </summary>
    public virtual IList<ResponseItemModel<T>> Items { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ResponseMapModel() => Items = [];
  }
}
