using System.Collections.Generic;

namespace Terminal.Core.Models
{
  public class ResponseItemModel<T>
  {
    /// <summary>
    /// Item
    /// </summary>
    public virtual T Data { get; set; }

    /// <summary>
    /// Items
    /// </summary>
    public virtual IList<ErrorModel> Errors { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ResponseItemModel() => Errors = new List<ErrorModel>();
  }
}
