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
  public class ObservableTimeCollection : ObservableCollection<PointModel?>
  {
    /// <summary>
    /// Internal tracker to identify new or existing point in time
    /// </summary>
    protected IDictionary<long, int> Indices { get; set; }

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
    public void Add(PointModel item, TimeSpan? span)
    {
      var previous = this.LastOrDefault();

      if (span is not null && previous is not null)
      {
        var nextTime = item.Time.Round(span);
        var previousTime = previous?.Time.Round(span);

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
    public void Add(PointModel item, TimeSpan? span, bool combine)
    {
      if (span is null)
      {
        Add(item);
        return;
      }

      var currentTime = item.Time.Round(span);
      var previousTime = (item.Time - span.Value).Round(span);
      var currentGroup = Indices.TryGetValue(currentTime.Value.Ticks, out int currentIndex) ? this[currentIndex] : null;
      var previousGroup = Indices.TryGetValue(previousTime.Value.Ticks, out int previousIndex) ? this[previousIndex] : null;
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
    protected PointModel? CreateGroup(
      PointModel? currentPoint,
      PointModel? nextPoint,
      PointModel? previousPoint,
      TimeSpan? span)
    {
      if (nextPoint?.Ask is null && nextPoint?.Bid is null)
      {
        return null;
      }

      var nextGroup = nextPoint ?? new PointModel();
      var bar = currentPoint?.Bar ?? new BarModel();

      if (currentPoint is not null)
      {
        nextGroup.AskSize += currentPoint?.AskSize;
        nextGroup.BidSize += currentPoint?.BidSize;
        bar.Open = currentPoint?.Bar?.Open;
      }

      nextGroup.Ask = nextPoint?.Ask ?? nextPoint?.Bid;
      nextGroup.Bid = nextPoint?.Bid ?? nextPoint?.Ask;
      nextGroup.Price = nextGroup.Bid ?? nextGroup.Ask;

      bar.Close = nextGroup.Price;
      bar.Open = nextGroup.Bar?.Open ?? previousPoint?.Price ?? nextGroup.Price;
      bar.Low = Math.Min((nextGroup.Bar?.Low ?? nextGroup.Price).Value, nextGroup.Price.Value);
      bar.High = Math.Max((nextGroup.Bar?.High ?? nextGroup.Price).Value, nextGroup.Price.Value);

      nextGroup.Bar = bar;
      nextGroup.TimeFrame = span;
      nextGroup.Time = nextPoint?.Time.Round(span);

      return nextGroup;
    }
  }
}
