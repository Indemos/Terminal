using System;
using System.Reactive.Linq;
using Terminal.Core.CollectionSpace;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Tests.Collections
{
  public class TimeCollectionTests
  {
    [Fact]
    public void AddToEmpty()
    {
      var span = DateTime.Now;
      var item = new TimeModel { Id = "1", Time = span };
      var collection = new TimeCollection<ITimeModel>();

      collection.ItemStream.Take(1).Subscribe(o =>
      {
        Assert.Null(o.Previous);
        Assert.Equal(item, o.Next);
        Assert.Equal(ActionEnum.Create, o.Action);
      });

      collection.ItemsStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(collection.Items, o.Next);
        Assert.Equal(ActionEnum.Create, o.Action);
      });

      collection.Add(item);

      Assert.Null(collection[1]);
      Assert.Equal(item, collection[0]);
    }

    [Fact]
    public void AddSameTime()
    {
      var stamp = DateTime.Now;
      var span = TimeSpan.FromSeconds(10);
      var item = new TimeModel { Id = "1", Time = stamp, TimeFrame = span };
      var itemNext = new TimeModel { Id = "2", Time = stamp, TimeFrame = span };
      var collection = new TimeCollection<ITimeModel> { item };

      collection.ItemStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(itemNext, o.Next);
        Assert.Equal(item, o.Previous);
        Assert.Equal(ActionEnum.Update, o.Action);
      });

      collection.ItemsStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(collection.Items, o.Next);
        Assert.Equal(ActionEnum.Update, o.Action);
      });

      collection.Add(itemNext);

      Assert.Null(collection[1]);
      Assert.Equal(itemNext, collection[0]);
    }

    [Fact]
    public void AddNextTime()
    {
      var stamp = DateTime.Now;
      var span = TimeSpan.FromSeconds(10);
      var item = new TimeModel { Id = "1", Time = stamp, TimeFrame = span };
      var itemNext = new TimeModel { Id = "2", Time = stamp.AddSeconds(20), TimeFrame = span };
      var collection = new TimeCollection<ITimeModel> { item };

      collection.ItemStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(itemNext, o.Next);
        Assert.Equal(item, o.Previous);
        Assert.Equal(ActionEnum.Create, o.Action);
      });

      collection.ItemsStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(collection.Items, o.Next);
        Assert.Equal(ActionEnum.Create, o.Action);
      });

      collection.Add(itemNext);

      Assert.Null(collection[2]);
      Assert.Equal(item, collection[0]);
      Assert.Equal(itemNext, collection[1]);
    }
  }
}
