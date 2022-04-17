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
    public override void Add(T item, TimeSpan? span)
    {
      var currentTime = item.Time.Round(span);
      var previousTime = (item.Time - span.Value).Round(span);
      var currentGroup = Indices.TryGetValue(currentTime.Value.Ticks, out int currentIndex) ? this[currentIndex] : default;
      var previousGroup = Indices.TryGetValue(previousTime.Value.Ticks, out int previousIndex) ? this[previousIndex] : default;

      if (currentGroup is not null)
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
      if (nextPoint.Ask is null && nextPoint.Bid is null)
      {
        return nextPoint;
      }

      var nextGroup = nextPoint.Clone() as IPointModel;

      nextGroup.AskSize ??= nextPoint.AskSize ?? 0.0;
      nextGroup.BidSize ??= nextPoint.BidSize ?? 0.0;

      nextGroup.Ask ??= nextPoint.Ask ?? nextPoint.Bid;
      nextGroup.Bid ??= nextPoint.Bid ?? nextPoint.Ask;

      nextGroup.Group.Open ??= previousPoint?.Last ?? nextGroup.Ask;
      nextGroup.Group.Close ??= previousPoint?.Last ?? nextGroup.Bid;
      nextGroup.Last ??= nextGroup.Group.Close;

      nextGroup.TimeFrame = span;
      nextGroup.Time = nextPoint.Time.Round(span);
      nextGroup.Group.Low ??= Math.Min(nextGroup.Bid.Value, nextGroup.Ask.Value);
      nextGroup.Group.High ??= Math.Max(nextGroup.Ask.Value, nextGroup.Bid.Value);

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
      previousGroup.Last = previousGroup.Group.Close =
        nextGroup.Last ??
        nextGroup.Group.Close ??
        nextGroup.Bid ?? 
        nextGroup.Ask;

      previousGroup.AskSize += nextGroup.AskSize ?? 0.0;
      previousGroup.BidSize += nextGroup.BidSize ?? 0.0;

      if (nextPoint.Ask is null || nextPoint.Bid is null)
      {
        return (T)previousGroup;
      }

      var min = Math.Min(nextGroup.Bid.Value, nextGroup.Ask.Value);
      var max = Math.Max(nextGroup.Ask.Value, nextGroup.Bid.Value);

      if (min < previousGroup.Group.Low)
      {
        previousGroup.Last = previousGroup.Group.Close = min;
      }

      if (max > previousGroup.Group.High)
      {
        previousGroup.Last = previousGroup.Group.Close = max;
      }

      previousGroup.Group.Low = Math.Min(previousGroup.Group.Low.Value, min);
      previousGroup.Group.High = Math.Max(previousGroup.Group.High.Value, max);

      return (T)previousGroup;
    }
  }
}
