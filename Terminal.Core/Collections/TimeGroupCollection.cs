using Terminal.Core.ExtensionSpace;
using Terminal.Core.ModelSpace;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace Terminal.Core.CollectionSpace
{
  /// <summary>
  /// Collection with aggregation by date and time
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class TimeGroupCollection<T> : TimeCollection<T>, ITimeCollection<T> where T : IPointModel
  {
    /// <summary>
    /// Internal tracker to identify new or existing point in time
    /// </summary>
    protected virtual IDictionary<long, int> Indices { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public TimeGroupCollection()
    {
      Indices = new Dictionary<long, int>();
    }

    /// <summary>
    /// Update or add item to the collection depending on its date and time 
    /// </summary>
    public override void Add(T item) => Add(item, item.TimeFrame);

    /// <summary>
    /// Update or add item to the collection depending on its date and time 
    /// </summary>
    public override void Add(T item, TimeSpan? span)
    {
      if (span is null)
      {
        base.Add(item, span);
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

        base.Add(group, span);

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
