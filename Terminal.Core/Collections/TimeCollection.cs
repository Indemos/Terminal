using Terminal.Core.ExtensionSpace;
using Terminal.Core.ModelSpace;
using System;
using System.Linq;

namespace Terminal.Core.CollectionSpace
{
  /// <summary>
  /// Collection with aggregation by date and time
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public interface ITimeCollection<T> : IIndexCollection<T> where T : ITimeModel
  {
    /// <summary>
    /// Update or add item to the collection depending on its date and time 
    /// </summary>
    void Add(T item, TimeSpan? span);
  }

  /// <summary>
  /// Collection with aggregation by date and time
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class TimeCollection<T> : IndexCollection<T>, ITimeCollection<T> where T : ITimeModel
  {
    /// <summary>
    /// Update or add item to the collection depending on its date and time 
    /// </summary>
    public override void Add(T item) => Add(item, item.TimeFrame);

    /// <summary>
    /// Update or add item to the collection depending on its date and time 
    /// </summary>
    public virtual void Add(T item, TimeSpan? span)
    {
      var previous = this.LastOrDefault();

      if (span is not null && previous is not null)
      {
        var nextTime = item.Time.Round(span);
        var previousTime = previous.Time.Round(span);

        if (Equals(previousTime, nextTime))
        {
          this[Count - 1] = item;
          return;
        }
      }

      base.Add(item);
    }
  }
}
