using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Terminal.Core.Collections
{
  /// <summary>
  /// Collection with aggregation by date and time
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ObservableTimeCollection<T> : ObservableCollection<T> where T : PointModel
  {
    /// <summary>
    /// Internal tracker to identify new or existing point in time
    /// </summary>
    protected virtual IDictionary<long, int> Indices { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ObservableTimeCollection()
    {
      Indices = new Dictionary<long, int>();
    }

    /// <summary>
    /// Update or add item to the collection depending on its date and time 
    /// </summary>
    /// <param name="item"></param>
    /// <param name="span"></param>
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

      Add(item);
    }

    /// <summary>
    /// Update or add item to the collection depending on its date and time 
    /// </summary>
    /// <param name="item"></param>
    /// <param name="span"></param>
    /// <param name="combine"></param>
    public virtual void Add(T item, TimeSpan? span, bool combine)
    {
      if (span is null)
      {
        Add(item);
        return;
      }

      var currentTime = item.Time.Round(span);
      var previousTime = (item.Time - span.Value).Round(span);
      var currentGroup = Indices.TryGetValue(currentTime.Value.Ticks, out int currentIndex) ? this[currentIndex] : default;
      var previousGroup = Indices.TryGetValue(previousTime.Value.Ticks, out int previousIndex) ? this[previousIndex] : default;
      var group = CreateGroup(currentGroup, item, previousGroup, span);

      if (group is not null)
      {
        if (currentGroup is not null)
        {
          this[currentIndex] = group;
          return;
        }

        Add(group);

        Indices[currentTime.Value.Ticks] = Count - 1;
      }
    }

    /// <summary>
    /// Group items by time
    /// </summary>
    /// <param name="currentPoint"></param>
    /// <param name="nextPoint"></param>
    /// <param name="previousPoint"></param>
    /// <param name="span"></param>
    /// <returns></returns>
    protected virtual T CreateGroup(T currentPoint, T nextPoint, T previousPoint, TimeSpan? span)
    {
      if (nextPoint.Ask is null && nextPoint.Bid is null)
      {
        return default;
      }

      var nextGroup = (T)nextPoint.Clone();

      if (currentPoint is not null)
      {
        nextGroup.AskSize += currentPoint.AskSize ?? 0.0;
        nextGroup.BidSize += currentPoint.BidSize ?? 0.0;
        nextGroup.Bar.Open = currentPoint.Bar.Open;
      }

      nextGroup.Ask = nextPoint.Ask ?? nextPoint.Bid;
      nextGroup.Bid = nextPoint.Bid ?? nextPoint.Ask;
      nextGroup.Last = nextGroup.Bid ?? nextGroup.Ask;

      nextGroup.Bar.Close = nextGroup.Last;
      nextGroup.Bar.Open = nextGroup.Bar.Open ?? previousPoint?.Last ?? nextGroup.Last;
      nextGroup.Bar.Low = Math.Min((nextGroup.Bar.Low ?? nextGroup.Last).Value, nextGroup.Last.Value);
      nextGroup.Bar.High = Math.Max((nextGroup.Bar.High ?? nextGroup.Last).Value, nextGroup.Last.Value);

      nextGroup.TimeFrame = span;
      nextGroup.Time = nextPoint.Time.Round(span);

      return nextGroup;
    }
  }
}
