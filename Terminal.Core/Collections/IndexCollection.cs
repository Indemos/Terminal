using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using Terminal.Core.EnumSpace;
using Terminal.Core.MessageSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.CollectionSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public interface IIndexCollection<T> : IList<T> where T : IBaseModel
  {
    /// <summary>
    /// Add to the collection
    /// </summary>
    /// <param name="items"></param>
    void Add(params T[] items);

    /// <summary>
    /// Update item in the collection
    /// </summary>
    /// <param name="index"></param>
    /// <param name="item"></param>
    void Update(int index, T item);

    /// <summary>
    /// Observable item
    /// </summary>
    ISubject<ITransactionMessage<T>> ItemStream { get; }

    /// <summary>
    /// Observable collection
    /// </summary>
    ISubject<ITransactionMessage<IList<T>>> ItemsStream { get; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class IndexCollection<T> : IIndexCollection<T> where T : IBaseModel
  {
    /// <summary>
    /// Immutable
    /// </summary>
    public virtual bool IsReadOnly => false;

    /// <summary>
    /// Count
    /// </summary>
    public virtual int Count => Items.Count;

    /// <summary>
    /// Items
    /// </summary>
    public virtual IList<T> Items { get; protected set; }

    /// <summary>
    /// Observable item
    /// </summary>
    public virtual ISubject<ITransactionMessage<T>> ItemStream { get; protected set; }

    /// <summary>
    /// Observable collection
    /// </summary>
    public virtual ISubject<ITransactionMessage<IList<T>>> ItemsStream { get; protected set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public IndexCollection()
    {
      Items = new List<T>();
      ItemStream = new Subject<ITransactionMessage<T>>();
      ItemsStream = new Subject<ITransactionMessage<IList<T>>>();
    }

    /// <summary>
    /// Search
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public virtual int IndexOf(T item) => Items.IndexOf(item);

    /// <summary>
    /// Search
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public virtual bool Contains(T item) => Items.Contains(item);

    /// <summary>
    /// Get enumerator
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerator<T> GetEnumerator() => Items.GetEnumerator();

    /// <summary>
    /// Get item by index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public virtual T this[int index]
    {
      get => Items.ElementAtOrDefault(index);
      set
      {
        var previous = Items[index];

        Items[index] = value;

        SendItemMessage(value, previous, ActionEnum.Update);
        SendItemsMessage(ActionEnum.Update);
      }
    }

    /// <summary>
    /// Clear collection
    /// </summary>
    public virtual void Clear()
    {
      Items.Clear();
      SendItemMessage(default, default, ActionEnum.Delete);
      SendItemsMessage(ActionEnum.Delete);
    }

    /// <summary>
    /// Add to the collection
    /// </summary>
    /// <param name="items"></param>
    public virtual void Add(params T[] items)
    {
      items.ForEach(o => Append(o));
      SendItemsMessage(ActionEnum.Create);
    }

    /// <summary>
    /// Add
    /// </summary>
    /// <param name="item"></param>
    public virtual void Add(T item)
    {
      Append(item);
      SendItemsMessage(ActionEnum.Create);
    }

    /// <summary>
    /// Copy
    /// </summary>
    /// <param name="items"></param>
    /// <param name="index"></param>
    public virtual void CopyTo(T[] items, int index)
    {
      foreach (var item in Items)
      {
        var previous = items[index];

        items.SetValue(item, index);
        SendItemMessage(items[index], previous, ActionEnum.Update);
        index++;
      }

      SendItemsMessage(ActionEnum.Update);
    }

    /// <summary>
    /// Insert
    /// </summary>
    /// <param name="index"></param>
    /// <param name="item"></param>
    public virtual void Insert(int index, T item)
    {
      var previous = Items.ElementAtOrDefault(index);

      Items.Insert(index, item);
      SendItemMessage(item, previous, ActionEnum.Create);
      SendItemsMessage(ActionEnum.Create);
    }

    /// <summary>
    /// Remove from the collection
    /// </summary>
    /// <param name="item"></param>
    public virtual bool Remove(T item)
    {
      var response = Items.Remove(item);

      SendItemMessage(default, item, ActionEnum.Delete);
      SendItemsMessage(ActionEnum.Delete);

      return response;
    }

    /// <summary>
    /// Remove at index
    /// </summary>
    /// <param name="index"></param>
    public virtual void RemoveAt(int index) => Remove(Items[index]);

    /// <summary>
    /// Update item in the collection
    /// </summary>
    /// <param name="index"></param>
    /// <param name="item"></param>
    public virtual void Update(int index, T item)
    {
      var previous = Items[index];

      if (previous is not null)
      {
        Items[index] = item;
        SendItemMessage(item, previous, ActionEnum.Update);
        SendItemsMessage(ActionEnum.Update);
      }
    }

    /// <summary>
    /// Internal append
    /// </summary>
    /// <param name="item"></param>
    protected virtual void Append(T item)
    {
      var previous = Items.LastOrDefault();

      Items.Add(item);
      SendItemMessage(item, previous, ActionEnum.Create);
    }

    /// <summary>
    /// Send item message
    /// </summary>
    /// <param name="next"></param>
    /// <param name="previous"></param>
    /// <param name="action"></param>
    protected virtual void SendItemMessage(T next, T previous, ActionEnum action)
    {
      var itemMessage = new TransactionMessage<T>
      {
        Next = next,
        Previous = previous,
        Action = action
      };

      ItemStream.OnNext(itemMessage);
    }

    /// <summary>
    /// Send collection message
    /// </summary>
    /// <param name="action"></param>
    protected virtual void SendItemsMessage(ActionEnum action)
    {
      var collectionMessage = new TransactionMessage<IList<T>>
      {
        Next = Items,
        Action = action
      };

      ItemsStream.OnNext(collectionMessage);
    }

    /// <summary>
    /// Get enumerator
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
  }
}
