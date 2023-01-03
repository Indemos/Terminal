using System;
using Terminal.Core.CollectionSpace;
using Terminal.Core.ExtensionSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Tests.Collections
{
  public class TimeGroupCollectionTests
  {
    IPointModel _point;

    public TimeGroupCollectionTests()
    {
      _point = new PointModel
      {
        Time = DateTime.Now,
        Ask = 200,
        Bid = 100,
        AskSize = 500,
        BidSize = 1000,
        TimeFrame = TimeSpan.FromSeconds(5),
        Bar = new PointBarModel
        {
          Low = 10,
          High = 100,
          Open = 20,
          Close = 50
        }
      };
    }

    [Fact]
    public void SkipMissingBidAsk()
    {
      var span = TimeSpan.FromSeconds(1);
      var item = new PointModel
      {
        Id = "2",
        Time = _point.Time.Round(span),
        TimeFrame = span
      };

      var collection = new TimeGroupCollection<IPointModel> { _point };

      collection.Add(item);

      Assert.Null(collection[1]);
    }

    [Fact]
    public void AddSameTime()
    {
      var span = TimeSpan.FromSeconds(5);
      var item = new PointModel
      {
        Time = _point.Time,
        Ask = 300,
        Bid = 50,
        AskSize = 10,
        BidSize = 20,
        TimeFrame = _point.TimeFrame,
        Bar = new PointBarModel
        {
          Low = 5,
          High = 150,
          Open = 40,
          Close = 100
        }
      };

      var expectation = new PointModel
      {
        Id = item.Id,
        Time = _point.Time.Round(span),
        Ask = item.Ask,
        Bid = item.Bid,
        Last = item.Bid,
        AskSize = _point.AskSize + item.AskSize,
        BidSize = _point.BidSize + item.BidSize,
        TimeFrame = _point.TimeFrame,
        Bar = new PointBarModel
        {
          Id = item.Bar.Id,
          Low = item.Bar.Low,
          High = item.Bar.High,
          Open = _point.Bar.Open,
          Close = item.Bid
        }
      };

      var collection = new TimeGroupCollection<IPointModel> { _point };

      collection.Add(item, span);

      Assert.Null(collection[1]);
      Assert.Equal(expectation.Time, collection[0].Time);
      Assert.Equal(expectation.TimeFrame, collection[0].TimeFrame);
      Assert.Equal(expectation.Ask, collection[0].Ask);
      Assert.Equal(expectation.Bid, collection[0].Bid);
      Assert.Equal(expectation.AskSize, collection[0].AskSize);
      Assert.Equal(expectation.BidSize, collection[0].BidSize);
      Assert.Equal(expectation.Last, collection[0].Last);
      Assert.Equal(expectation.Bar.Low, collection[0].Bar.Low);
      Assert.Equal(expectation.Bar.High, collection[0].Bar.High);
      Assert.Equal(expectation.Bar.Open, collection[0].Bar.Open);
      Assert.Equal(expectation.Bar.Close, collection[0].Bar.Close);
    }
  }
}
