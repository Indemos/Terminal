using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Terminal.Core.Collections
{
  public interface IGroup
  {
    /// <summary>
    /// Group index
    /// </summary>
    /// <returns></returns>
    long GetIndex();

    /// <summary>
    /// Grouping implementation
    /// </summary>
    /// <param name="previous"></param>
    /// <returns></returns>
    IGroup Update(IGroup previous);
  }

  public class ObservableGroupCollection<T> : ObservableCollection<T> where T : IGroup
  {
    /// <summary>
    /// Groups
    /// </summary>
    protected virtual IDictionary<long, int> Groups { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ObservableGroupCollection()
    {
      Groups = new Dictionary<long, int>();
    }

    /// <summary>
    /// Grouping implementation
    /// </summary>
    /// <param name="item"></param>
    /// <param name="span"></param>
    public virtual void Add(IGroup item, TimeSpan? span)
    {
      var index = item.GetIndex();

      if (Groups.TryGetValue(index, out var position))
      {
        this[position] = (T)item.Update(this[position]);
        return;
      }

      Groups[index] = Count;

      Add((T)item.Update(null));
    }
  }
}
