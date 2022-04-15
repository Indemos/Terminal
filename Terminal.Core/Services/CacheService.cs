using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Terminal.Core.ServiceSpace
{
  /// <summary>
  /// Cache
  /// </summary>
  public interface ICacheService<TKey, TValue>
  {
    /// <summary>
    /// Single instance
    /// </summary>
    IDictionary<TKey, TValue> Cache { get; }
  }

  /// <summary>
  /// Cache
  /// </summary>
  public class CacheService<TKey, TValue> : ICacheService<TKey, TValue>
  {
    /// <summary>
    /// Logger instance
    /// </summary>
    public virtual IDictionary<TKey, TValue> Cache { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    public CacheService() => Cache = new ConcurrentDictionary<TKey, TValue>();
  }
}
