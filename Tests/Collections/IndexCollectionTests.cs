using System;
using System.Reactive.Linq;
using Terminal.Core.CollectionSpace;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Tests.Collections
{
  public class IndexCollectionTests
  {
    [Fact]
    public void Clear()
    {
      var item = new BaseModel { Id = "1" };
      var collection = new IndexCollection<IBaseModel> { item };

      collection.ItemStream.Take(1).Subscribe(o =>
      {
        Assert.Null(o.Next);
        Assert.Null(o.Previous);
        Assert.Equal(ActionEnum.Delete, o.Action);
      });

      collection.ItemsStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(collection.Items, o.Next);
        Assert.Equal(ActionEnum.Delete, o.Action);
      });

      collection.Clear();

      Assert.Empty(collection);
    }

    [Fact]
    public void Add()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var collection = new IndexCollection<IBaseModel>();

      Assert.Empty(collection);

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

      collection.Add(new[] { itemNext });

      Assert.Null(collection[2]);
      Assert.Equal(item, collection[0]);
      Assert.Equal(itemNext, collection[1]);
    }

    [Fact]
    public void Insert()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var collection = new IndexCollection<IBaseModel> { item };

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

      collection.Insert(0, itemNext);

      Assert.Null(collection[2]);
      Assert.Equal(itemNext, collection[0]);
      Assert.Equal(item, collection[1]);
    }

    [Fact]
    public void GetByIndex()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var collection = new IndexCollection<IBaseModel> { item, itemNext };

      Assert.Null(collection[2]);
      Assert.Equal(item, collection[0]);
      Assert.Equal(itemNext, collection[1]);
    }

    [Fact]
    public void GetIndex()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var collection = new IndexCollection<IBaseModel> { item, itemNext };

      Assert.Equal(0, collection.IndexOf(item));
      Assert.Equal(1, collection.IndexOf(itemNext));
    }

    [Fact]
    public void Contains()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var collection = new IndexCollection<IBaseModel> { item };
      var itemResponse = collection.Contains(item);
      var itemNextResponse = collection.Contains(itemNext);

      Assert.True(itemResponse);
      Assert.False(itemNextResponse);
    }

    [Fact]
    public void Remove()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var collection = new IndexCollection<IBaseModel> { item, itemNext };

      collection.ItemStream.Take(1).Subscribe(o =>
      {
        Assert.Null(o.Next);
        Assert.Equal(itemNext, o.Previous);
        Assert.Equal(ActionEnum.Delete, o.Action);
      });

      collection.ItemsStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(collection.Items, o.Next);
        Assert.Equal(ActionEnum.Delete, o.Action);
      });

      collection.Remove(itemNext);

      Assert.Single(collection);
      Assert.Contains(item, collection);
      Assert.DoesNotContain(itemNext, collection);
    }

    [Fact]
    public void RemoveAt()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var collection = new IndexCollection<IBaseModel> { item, itemNext };

      collection.ItemStream.Take(1).Subscribe(o =>
      {
        Assert.Null(o.Next);
        Assert.Equal(itemNext, o.Previous);
        Assert.Equal(ActionEnum.Delete, o.Action);
      });

      collection.ItemsStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(collection.Items, o.Next);
        Assert.Equal(ActionEnum.Delete, o.Action);
      });

      collection.RemoveAt(1);

      Assert.Single(collection);
      Assert.Contains(item, collection);
      Assert.DoesNotContain(itemNext, collection);
    }

    [Fact]
    public void Update()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var collection = new IndexCollection<IBaseModel> { item };

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

      collection.Update(0, itemNext);

      Assert.Single(collection);
      Assert.Contains(itemNext, collection);
      Assert.DoesNotContain(item, collection);
    }
  }
}
