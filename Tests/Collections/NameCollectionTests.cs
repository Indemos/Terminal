using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Terminal.Core.CollectionSpace;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Tests.Collections
{
  public class NameCollectionTests
  {
    [Fact]
    public void Clear()
    {
      var item = new BaseModel { Id = "1" };
      var collection = new NameCollection<string, IBaseModel> { [item.Id] = item };

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
      var itemAction = new BaseModel { Id = "3" };
      var collection = new NameCollection<string, IBaseModel> { [item.Id] = item };

      collection.ItemStream.Take(1).Subscribe(o =>
      {
        Assert.Null(o.Previous);
        Assert.Equal(itemNext, o.Next);
        Assert.Equal(ActionEnum.Create, o.Action);
      });

      collection.ItemsStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(collection.Items, o.Next);
        Assert.Equal(ActionEnum.Create, o.Action);
      });

      collection.Add(itemNext.Id, itemNext, ActionEnum.Create);

      Assert.Equal(2, collection.Count);
      Assert.Equal(item, collection[item.Id]);
      Assert.Equal(itemNext, collection[itemNext.Id]);

      collection.Add(KeyValuePair.Create(item.Id, itemNext as IBaseModel));

      Assert.Equal(2, collection.Count);
      Assert.Equal(itemNext, collection[item.Id]);
      Assert.Equal(itemNext, collection[itemNext.Id]);

      collection.Add(itemAction.Id, itemAction);

      Assert.Equal(3, collection.Count);
      Assert.Equal(itemNext, collection[item.Id]);
      Assert.Equal(itemNext, collection[itemNext.Id]);
      Assert.Equal(itemAction, collection[itemAction.Id]);
    }

    [Fact]
    public void GetByIndex()
    {
      var item = new BaseModel { Id = "1" };
      var collection = new NameCollection<string, IBaseModel> { [item.Id] = item };

      Assert.Null(collection[string.Empty]);
      Assert.Equal(item, collection[item.Id]);
    }

    [Fact]
    public void Contains()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var itemPair = KeyValuePair.Create(item.Id, item as IBaseModel);
      var itemNextPair = KeyValuePair.Create(itemNext.Id, itemNext as IBaseModel);
      var collection = new NameCollection<string, IBaseModel>();

      collection.Add(itemPair);

      var itemResponse = collection.Contains(itemPair);
      var itemNextResponse = collection.Contains(itemNextPair);

      Assert.True(itemResponse);
      Assert.False(itemNextResponse);
    }

    [Fact]
    public void ContainsKey()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var itemPair = KeyValuePair.Create(item.Id, item as IBaseModel);
      var itemNextPair = KeyValuePair.Create(itemNext.Id, itemNext as IBaseModel);
      var collection = new NameCollection<string, IBaseModel>();

      collection.Add(itemPair);

      var itemResponse = collection.ContainsKey(itemPair.Key);
      var itemNextResponse = collection.ContainsKey(itemNextPair.Key);

      Assert.True(itemResponse);
      Assert.False(itemNextResponse);
    }

    [Fact]
    public void Remove()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var itemPair = KeyValuePair.Create(item.Id, item as IBaseModel);
      var itemNextPair = KeyValuePair.Create(itemNext.Id, itemNext as IBaseModel);
      var collection = new NameCollection<string, IBaseModel> { itemPair, itemNextPair };

      collection.ItemStream.Take(1).Subscribe(o =>
      {
        Assert.Null(o.Next);
        Assert.Equal(item, o.Previous);
        Assert.Equal(ActionEnum.Delete, o.Action);
      });

      collection.ItemsStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(collection.Items, o.Next);
        Assert.Equal(ActionEnum.Delete, o.Action);
      });

      var itemResponse = collection.Remove(itemPair);
      var itemNextResponse = collection.Remove(itemNextPair.Key);
      var itemNoneResponse = collection.Remove(string.Empty);

      Assert.True(itemResponse);
      Assert.True(itemNextResponse);
      Assert.False(itemNoneResponse);
    }

    [Fact]
    public void Update()
    {
      var item = new BaseModel { Id = "1" };
      var itemNext = new BaseModel { Id = "2" };
      var collection = new NameCollection<string, IBaseModel>();

      collection.ItemStream.Take(1).Subscribe(o =>
      {
        Assert.Null(o.Previous);
        Assert.Equal(item, o.Next);
        Assert.Equal(ActionEnum.Update, o.Action);
      });

      collection.ItemsStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(collection.Items, o.Next);
        Assert.Equal(ActionEnum.Update, o.Action);
      });

      collection[item.Id] = item;

      Assert.Single(collection);
      Assert.Equal(item, collection[item.Id]);

      collection.ItemStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(item, o.Previous);
        Assert.Equal(itemNext, o.Next);
        Assert.Equal(ActionEnum.Update, o.Action);
      });

      collection.ItemsStream.Take(1).Subscribe(o =>
      {
        Assert.Equal(collection.Items, o.Next);
        Assert.Equal(ActionEnum.Update, o.Action);
      });

      collection[item.Id] = itemNext;

      Assert.Single(collection);
      Assert.Equal(itemNext, collection[item.Id]);
    }
  }
}
