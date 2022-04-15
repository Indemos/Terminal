using Terminal.Core.ExtensionSpace;
using Terminal.Core.ModelSpace;
using System;
using System.Collections.Generic;

namespace Terminal.Core.CollectionSpace
{
  /// <summary>
  /// Collection with aggregation by date and time
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public interface ITimeSpanCollection<T> : ITimeCollection<T> where T : IPointModel
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
  public class TimeSpanCollection<T> : TimeCollection<T>, ITimeSpanCollection<T> where T : IPointModel
  {
    /// <summary>
    /// Internal tracker to identify new or existing point in time
    /// </summary>
    public virtual IDictionary<long, int> Indices { get; protected set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public TimeSpanCollection()
    {
      Indices = new Dictionary<long, int>();
    }

    /// <summary>
    /// Update or add item to the collection depending on its date and time 
    /// </summary>
    public virtual void Add(T item, TimeSpan? span)
    {
      var currentTime = item.Time.Round(span);
      var previousTime = (item.Time - span.Value).Round(span);
      var currentGroup = Indices.TryGetValue(currentTime.Value.Ticks, out int currentIndex) ? this[currentIndex] : default;
      var previousGroup = Indices.TryGetValue(previousTime.Value.Ticks, out int previousIndex) ? this[previousIndex] : default;

      if (currentGroup != null)
      {
        this[currentIndex] = UpdateGroup(item, currentGroup);
        return;
      }

      base.Add(CreateGroup(item, previousGroup, span));

      Indices[currentTime.Value.Ticks] = Count - 1;
    }

    /// <summary>
    /// Group items by time
    /// </summary>
    /// <param name="nextPoint"></param>
    /// <param name="previousPoint"></param>
    /// <param name="span"></param>
    /// <returns></returns>
    protected T CreateGroup(T nextPoint, T previousPoint, TimeSpan? span)
    {
      if (nextPoint.Ask == null && nextPoint.Bid == null)
      {
        return nextPoint;
      }

      var nextGroup = nextPoint.Clone() as IPointModel;

      nextGroup.AskSize ??= nextPoint.AskSize ?? 0.0;
      nextGroup.BidSize ??= nextPoint.BidSize ?? 0.0;

      nextGroup.Ask ??= nextPoint.Ask ?? nextPoint.Bid;
      nextGroup.Bid ??= nextPoint.Bid ?? nextPoint.Ask;

      nextGroup.Bar.Open ??= previousPoint?.Last ?? nextGroup.Ask;
      nextGroup.Bar.Close ??= previousPoint?.Last ?? nextGroup.Bid;
      nextGroup.Last ??= nextGroup.Bar.Close;

      nextGroup.TimeFrame = span;
      nextGroup.Time = nextPoint.Time.Round(span);
      nextGroup.Bar.Low ??= Math.Min(nextGroup.Bid.Value, nextGroup.Ask.Value);
      nextGroup.Bar.High ??= Math.Max(nextGroup.Ask.Value, nextGroup.Bid.Value);

      return (T)nextGroup;
    }

    /// <summary>
    /// Group items by time
    /// </summary>
    /// <param name="nextPoint"></param>
    /// <param name="previousPoint"></param>
    /// <returns></returns>
    protected T UpdateGroup(T nextPoint, T previousPoint)
    {
      var nextGroup = nextPoint as IPointModel;
      var previousGroup = previousPoint as IPointModel;

      previousGroup.Ask = nextGroup.Ask ?? nextGroup.Bid;
      previousGroup.Bid = nextGroup.Bid ?? nextGroup.Ask;
      previousGroup.Last = previousGroup.Bar.Close =
        nextGroup.Last ??
        nextGroup.Bar.Close ??
        nextGroup.Bid ?? 
        nextGroup.Ask;

      previousGroup.AskSize += nextGroup.AskSize ?? 0.0;
      previousGroup.BidSize += nextGroup.BidSize ?? 0.0;

      if (nextPoint.Ask == null || nextPoint.Bid == null)
      {
        return (T)previousGroup;
      }

      var min = Math.Min(nextGroup.Bid.Value, nextGroup.Ask.Value);
      var max = Math.Max(nextGroup.Ask.Value, nextGroup.Bid.Value);

      if (min < previousGroup.Bar.Low)
      {
        previousGroup.Last = previousGroup.Bar.Close = min;
      }

      if (max > previousGroup.Bar.High)
      {
        previousGroup.Last = previousGroup.Bar.Close = max;
      }

      previousGroup.Bar.Low = Math.Min(previousGroup.Bar.Low.Value, min);
      previousGroup.Bar.High = Math.Max(previousGroup.Bar.High.Value, max);

      return (T)previousGroup;
    }
  }
}
