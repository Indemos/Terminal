using Core.CollectionSpace;
using Core.ModelSpace;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Evaluation
{
  public class TimeSpanCollectionTests
  {
    [Fact]
    public void ShouldGroupSecondsByOneSecond()
    {
      var inputs = new List<IPointModel>
      {
        new PointModel { Ask = -1, Bid = 0, Time = new DateTime(2000, 1, 1, 0, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 5.55, Bid = 5.50, Time = new DateTime(2000, 1, 1, 0, 0, 1), Bar = new PointBarModel() },
        new PointModel { Ask = 0, Bid = 0, Time = new DateTime(2000, 1, 1, 0, 0, 2), Bar = new PointBarModel() },
        new PointModel { Ask = 4.75, Bid = 4.70, Time = new DateTime(2000, 1, 1, 0, 0, 3), Bar = new PointBarModel() },
        new PointModel { Ask = 6.505, Bid = 5.90, Time = new DateTime(2000, 1, 1, 0, 0, 4), Bar = new PointBarModel() },
        new PointModel { Ask = 6.00, Bid = 5.80, Time = new DateTime(2000, 1, 1, 0, 0, 4, 1), Bar = new PointBarModel() }
      };

      var groups = CreateCollection(inputs, TimeSpan.FromSeconds(1));
      var startTime = inputs.First().Time.Value;
      var roundTime = new DateTime(
        startTime.Year,
        startTime.Month,
        startTime.Day,
        startTime.Hour,
        startTime.Minute, 0);

      // Compare count

      Assert.Equal(5, groups.Count);

      // Compare times

      Assert.Equal(roundTime, groups[0].Time);
      Assert.Equal(roundTime.AddSeconds(1), groups[1].Time);
      Assert.Equal(roundTime.AddSeconds(2), groups[2].Time);
      Assert.Equal(roundTime.AddSeconds(3), groups[3].Time);
      Assert.Equal(roundTime.AddSeconds(4), groups[4].Time);

      // Compare lows

      Assert.Equal(inputs[0].Ask.Value, groups[0].Bar.Low.Value, 3);
      Assert.Equal(inputs[1].Bid.Value, groups[1].Bar.Low.Value, 3);
      Assert.Equal(inputs[2].Bid.Value, groups[2].Bar.Low.Value, 3);
      Assert.Equal(inputs[3].Bid.Value, groups[3].Bar.Low.Value, 3);
      Assert.Equal(inputs[5].Bid.Value, groups[4].Bar.Low.Value, 3);

      // Compare highs

      Assert.Equal(inputs[0].Bid.Value, groups[0].Bar.High.Value, 3);
      Assert.Equal(inputs[1].Ask.Value, groups[1].Bar.High.Value, 3);
      Assert.Equal(inputs[2].Ask.Value, groups[2].Bar.High.Value, 3);
      Assert.Equal(inputs[3].Ask.Value, groups[3].Bar.High.Value, 3);
      Assert.Equal(inputs[4].Ask.Value, groups[4].Bar.High.Value, 3);

      // Compare opens

      Assert.Equal(inputs[0].Ask.Value, groups[0].Bar.Open.Value, 3);
      Assert.Equal(inputs[1].Ask.Value, groups[1].Bar.Open.Value, 3);
      Assert.Equal(inputs[2].Ask.Value, groups[2].Bar.Open.Value, 3);
      Assert.Equal(inputs[3].Ask.Value, groups[3].Bar.Open.Value, 3);
      Assert.Equal(inputs[4].Ask.Value, groups[4].Bar.Open.Value, 3);

      // Compare closes

      Assert.Equal(inputs[0].Ask.Value, groups[0].Bar.Close.Value, 3);
      Assert.Equal(inputs[1].Ask.Value, groups[1].Bar.Close.Value, 3);
      Assert.Equal(inputs[2].Ask.Value, groups[2].Bar.Close.Value, 3);
      Assert.Equal(inputs[3].Ask.Value, groups[3].Bar.Close.Value, 3);
      Assert.Equal(inputs[5].Ask.Value, groups[4].Bar.Close.Value, 3);
    }

    [Fact]
    public void ShouldGroupSecondsByTwoSeconds()
    {
      var inputs = new List<IPointModel>
      {
        new PointModel { Ask = -1, Bid = 0, Time = new DateTime(2000, 1, 1, 0, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 5.50, Bid = 5.50, Time = new DateTime(2000, 1, 1, 0, 0, 1), Bar = new PointBarModel() },
        new PointModel { Ask = 0, Bid = 0, Time = new DateTime(2000, 1, 1, 0, 0, 2), Bar = new PointBarModel() },
        new PointModel { Ask = 4.75, Bid = 4.70, Time = new DateTime(2000, 1, 1, 0, 0, 3), Bar = new PointBarModel() },
        new PointModel { Ask = 6.505, Bid = 5.90, Time = new DateTime(2000, 1, 1, 0, 0, 4), Bar = new PointBarModel() },
        new PointModel { Ask = 6.00, Bid = 5.80, Time = new DateTime(2000, 1, 1, 0, 0, 4, 1), Bar = new PointBarModel() }
      };

      var groups = CreateCollection(inputs, TimeSpan.FromSeconds(2));
      var startTime = inputs.First().Time.Value;
      var roundTime = new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, startTime.Minute, 0);

      // Compare count

      Assert.Equal(3, groups.Count);

      // Compare times

      Assert.Equal(roundTime, groups[0].Time);
      Assert.Equal(roundTime.AddSeconds(2), groups[1].Time);
      Assert.Equal(roundTime.AddSeconds(4), groups[2].Time);

      // Compare lows

      Assert.Equal(inputs[0].Ask.Value, groups[0].Bar.Low.Value, 3);
      Assert.Equal(inputs[2].Bid.Value, groups[1].Bar.Low.Value, 3);
      Assert.Equal(inputs[5].Bid.Value, groups[2].Bar.Low.Value, 3);

      // Compare highs

      Assert.Equal(inputs[1].Ask.Value, groups[0].Bar.High.Value, 3);
      Assert.Equal(inputs[3].Ask.Value, groups[1].Bar.High.Value, 3);
      Assert.Equal(inputs[4].Ask.Value, groups[2].Bar.High.Value, 3);

      // Compare opens

      Assert.Equal(inputs[0].Ask.Value, groups[0].Bar.Open.Value, 3);
      Assert.Equal(inputs[2].Ask.Value, groups[1].Bar.Open.Value, 3);
      Assert.Equal(inputs[4].Ask.Value, groups[2].Bar.Open.Value, 3);

      // Compare closes

      Assert.Equal(inputs[1].Ask.Value, groups[0].Bar.Close.Value, 3);
      Assert.Equal(inputs[3].Ask.Value, groups[1].Bar.Close.Value, 3);
      Assert.Equal(inputs[5].Ask.Value, groups[2].Bar.Close.Value, 3);
    }

    [Fact]
    public void ShouldGroupHoursByOneSecond()
    {
      var inputs = new List<IPointModel>
      {
        new PointModel { Ask = -1, Bid = 0, Time = new DateTime(2000, 1, 1, 1, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 7.35, Bid = 7.05, Time = new DateTime(2000, 1, 1, 2, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 0, Bid = 0, Time = new DateTime(2000, 1, 1, 3, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 6.95, Bid = 6.25, Time = new DateTime(2000, 1, 1, 4, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 6.45, Bid = 6.30, Time = new DateTime(2000, 1, 1, 5, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 6.35, Bid = 6.25, Time = new DateTime(2000, 1, 1, 5, 0, 1), Bar = new PointBarModel() }
      };

      var groups = CreateCollection(inputs, TimeSpan.FromSeconds(1));

      // Compare count

      Assert.Equal(6, groups.Count);

      // Compare times

      Assert.Equal(inputs[0].Time, groups[0].Time);
      Assert.Equal(inputs[1].Time, groups[1].Time);
      Assert.Equal(inputs[2].Time, groups[2].Time);
      Assert.Equal(inputs[3].Time, groups[3].Time);
      Assert.Equal(inputs[4].Time, groups[4].Time);
      Assert.Equal(inputs[5].Time, groups[5].Time);

      // Compare lows

      Assert.Equal(inputs[0].Ask.Value, groups[0].Bar.Low.Value, 3);
      Assert.Equal(inputs[1].Bid.Value, groups[1].Bar.Low.Value, 3);
      Assert.Equal(inputs[2].Bid.Value, groups[2].Bar.Low.Value, 3);
      Assert.Equal(inputs[3].Bid.Value, groups[3].Bar.Low.Value, 3);
      Assert.Equal(inputs[4].Bid.Value, groups[4].Bar.Low.Value, 3);
      Assert.Equal(inputs[5].Bid.Value, groups[5].Bar.Low.Value, 3);

      // Compare highs

      Assert.Equal(inputs[0].Bid.Value, groups[0].Bar.High.Value, 3);
      Assert.Equal(inputs[1].Ask.Value, groups[1].Bar.High.Value, 3);
      Assert.Equal(inputs[2].Ask.Value, groups[2].Bar.High.Value, 3);
      Assert.Equal(inputs[3].Ask.Value, groups[3].Bar.High.Value, 3);
      Assert.Equal(inputs[4].Ask.Value, groups[4].Bar.High.Value, 3);
      Assert.Equal(inputs[5].Ask.Value, groups[5].Bar.High.Value, 3);

      // Compare opens

      Assert.Equal(inputs[0].Ask.Value, groups[0].Bar.Open.Value, 3);
      Assert.Equal(inputs[1].Ask.Value, groups[1].Bar.Open.Value, 3);
      Assert.Equal(inputs[2].Ask.Value, groups[2].Bar.Open.Value, 3);
      Assert.Equal(inputs[3].Ask.Value, groups[3].Bar.Open.Value, 3);
      Assert.Equal(inputs[4].Ask.Value, groups[4].Bar.Open.Value, 3);
      Assert.Equal(inputs[5].Ask.Value, groups[5].Bar.Open.Value, 3);

      // Compare closes

      Assert.Equal(inputs[0].Ask.Value, groups[0].Bar.Close.Value, 3);
      Assert.Equal(inputs[1].Ask.Value, groups[1].Bar.Close.Value, 3);
      Assert.Equal(inputs[2].Ask.Value, groups[2].Bar.Close.Value, 3);
      Assert.Equal(inputs[3].Ask.Value, groups[3].Bar.Close.Value, 3);
      Assert.Equal(inputs[4].Ask.Value, groups[4].Bar.Close.Value, 3);
      Assert.Equal(inputs[5].Ask.Value, groups[5].Bar.Close.Value, 3);
    }

    [Fact]
    public void ShouldGroupHoursByDays()
    {
      var inputs = new List<IPointModel>
      {
        new PointModel { Ask = -1, Bid = 0, Time = new DateTime(2000, 1, 1, 1, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 7.35, Bid = 7.05, Time = new DateTime(2000, 1, 1, 2, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 0, Bid = 0, Time = new DateTime(2000, 1, 1, 3, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 6.95, Bid = 6.25, Time = new DateTime(2000, 1, 2, 0, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 6.45, Bid = 6.30, Time = new DateTime(2000, 1, 3, 1, 0, 0), Bar = new PointBarModel() },
        new PointModel { Ask = 6.35, Bid = 6.25, Time = new DateTime(2000, 1, 3, 1, 0, 1), Bar = new PointBarModel() }
      };

      var groups = CreateCollection(inputs, TimeSpan.FromDays(1));
      var startTime = inputs.First().Time.Value;
      var roundTime = new DateTime(startTime.Year, startTime.Month, startTime.Day);

      // Compare count

      Assert.Equal(3, groups.Count);

      // Compare times

      Assert.Equal(roundTime, groups[0].Time);
      Assert.Equal(roundTime.AddDays(1), groups[1].Time);
      Assert.Equal(roundTime.AddDays(2), groups[2].Time);

      // Compare lows

      Assert.Equal(inputs[0].Ask.Value, groups[0].Bar.Low.Value, 3);
      Assert.Equal(inputs[3].Bid.Value, groups[1].Bar.Low.Value, 3);
      Assert.Equal(inputs[5].Bid.Value, groups[2].Bar.Low.Value, 3);

      // Compare highs

      Assert.Equal(inputs[1].Ask.Value, groups[0].Bar.High.Value, 3);
      Assert.Equal(inputs[3].Ask.Value, groups[1].Bar.High.Value, 3);
      Assert.Equal(inputs[4].Ask.Value, groups[2].Bar.High.Value, 3);

      // Compare opens

      Assert.Equal(inputs[0].Ask.Value, groups[0].Bar.Open.Value, 3);
      Assert.Equal(inputs[3].Ask.Value, groups[1].Bar.Open.Value, 3);
      Assert.Equal(inputs[4].Ask.Value, groups[2].Bar.Open.Value, 3);

      // Compare closes

      Assert.Equal(inputs[2].Ask.Value, groups[0].Bar.Close.Value, 3);
      Assert.Equal(inputs[3].Ask.Value, groups[1].Bar.Close.Value, 3);
      Assert.Equal(inputs[5].Bid.Value, groups[2].Bar.Close.Value, 3);
    }

    private ITimeSpanCollection<IPointModel> CreateCollection(IEnumerable<IPointModel> inputs, TimeSpan span)
    {
      var groupCollection = new TimeSpanCollection<IPointModel>();

      foreach (var second in inputs)
      {
        groupCollection.Add(second, span);
      }

      return groupCollection;
    }
  }
}
