using System;
using System.Collections.Generic;

namespace Terminal.Core.Collections
{
  public class ConcurrentGroup<T> : SynchronizedCollection<T> where T : class, ICloneable, IGroup<T>
  {
    private object _sync;

    /// <summary>
    /// Groups
    /// </summary>
    protected IDictionary<long, int> _groups;

    /// <summary>
    /// Constructor
    /// </summary>
    public ConcurrentGroup()
    {
      _sync = new object();
      _groups = new Dictionary<long, int>();
    }

    /// <summary>
    /// Grouping implementation
    /// </summary>
    /// <param name="item"></param>
    /// <param name="span"></param>
    public virtual void Add(T item, TimeSpan? span)
    {
      lock (_sync)
      {
        var index = item.GetIndex();

        if (_groups.TryGetValue(index, out var position))
        {
          this[position] = (item.Clone() as T).Update(this[position]);
          return;
        }

        _groups[index] = Count;

        Add((item.Clone() as T).Update(null));
      }
    }
  }
}
