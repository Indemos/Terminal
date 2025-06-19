using System;
using System.Collections.Generic;
using System.Linq;

namespace Terminal.Core.Collections
{
  public class ConcurrentGroup<T> : SynchronizedCollection<T> where T : class, ICloneable, IGroup<T>
  {
    private object sync;

    /// <summary>
    /// Groups
    /// </summary>
    protected IDictionary<long, int> groups;

    /// <summary>
    /// Constructor
    /// </summary>
    public ConcurrentGroup()
    {
      sync = new object();
      groups = new Dictionary<long, int>();
    }

    /// <summary>
    /// Grouping implementation
    /// </summary>
    /// <param name="item"></param>
    /// <param name="span"></param>
    public virtual void Add(T item, TimeSpan? span)
    {
      lock (sync)
      {
        var index = item.GetIndex();

        if (groups.TryGetValue(index, out var position) && this.ElementAtOrDefault(position) is not null)
        {
          this[position] = (item.Clone() as T).Update(this[position]);
          return;
        }

        groups[index] = Count;

        Add((item.Clone() as T).Update(null));
      }
    }
  }
}
