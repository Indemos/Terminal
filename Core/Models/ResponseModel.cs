using System.Collections.Generic;

namespace Core.ModelSpace
{
  /// <summary>
  /// Generic response model
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ResponseModel<T>
  {
    /// <summary>
    /// Number of items the query
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Items per page returned in the request
    /// </summary>
    public List<T> Items { get; set; } = new List<T>();

    /// <summary>
    /// List of server errors
    /// </summary>
    public List<string> Errors { get; set; } = new List<string>();
  }
}
