using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Core.ModelSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  public interface ICacheService<TKey, TValue>
  {
    /// <summary>
    /// Single instance
    /// </summary>
    IDictionary<TKey, TValue> Cache { get; }
  }

  /// <summary>
  /// Service to track account changes, including equity and quotes
  /// </summary>
  public class CacheService<TKey, TValue> : ICacheService<TKey, TValue>
  {
    /// <summary>
    /// Logger instance
    /// </summary>
    public IDictionary<TKey, TValue> Cache { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    public CacheService() => Cache = new ConcurrentDictionary<TKey, TValue>();
  }
}
