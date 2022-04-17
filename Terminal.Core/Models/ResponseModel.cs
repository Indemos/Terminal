using System.Collections.Generic;

namespace Terminal.Core.ModelSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public interface IResponseModel<T>
  {
    /// <summary>
    /// Number of items the query
    /// </summary>
    int Count { get; set; }

    /// <summary>
    /// List of server errors
    /// </summary>
    IList<T> Items { get; set; }

    /// <summary>
    /// Items per page returned in the request
    /// </summary>
    IList<string> Errors { get; set; }
  }

  /// <summary>
  /// Generic response model
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ResponseModel<T> : IResponseModel<T>
  {
    /// <summary>
    /// Number of items the query
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Items per page returned in the request
    /// </summary>
    public IList<T> Items { get; set; }

    /// <summary>
    /// List of server errors
    /// </summary>
    public IList<string> Errors { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ResponseModel()
    {
      Items = new List<T>();
      Errors = new List<string>();
    }
  }
}
