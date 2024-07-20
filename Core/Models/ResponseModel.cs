using System.Collections.Generic;

namespace Terminal.Core.Models
{
  public class ResponseModel<T>
  {
    /// <summary>
    /// Item
    /// </summary>
    public T Data { get; set; }

    /// <summary>
    /// Items
    /// </summary>
    public IList<ErrorModel> Errors { get; set; } = [];
  }
}
