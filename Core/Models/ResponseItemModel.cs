using System.Collections.Generic;

namespace Terminal.Core.Models
{
  public struct ResponseItemModel<T>
  {
    /// <summary>
    /// Item
    /// </summary>
    public T Data { get; set; }

    /// <summary>
    /// Items
    /// </summary>
    public IList<ErrorModel> Errors { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ResponseItemModel() => Errors = new List<ErrorModel>();
  }
}
