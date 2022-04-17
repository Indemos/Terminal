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
  public interface IIndexCollection<T> : IEnumerable<T> where T : IBaseModel
  {
    /// <summary>
    /// Get item by index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    T this[int index] { get; set; }

    /// <summary>
    /// Count
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Clear collection
    /// </summary>
    void Clear();

    /// <summary>
    /// Add to the collection
    /// </summary>
    /// <param name="items"></param>
    void Add(params T[] items);

    /// <summary>
    /// Remove from the collection
    /// </summary>
    /// <param name="item"></param>
    void Remove(T item);

    /// <summary>
    /// Update item in the collection
    /// </summary>
    /// <param name="item"></param>
    void Update(T item);

    /// <summary>
    /// Observable item
    /// </summary>
    ISubject<ITransactionMessage<T>> ItemStream { get; }

    /// <summary>
    /// Observable collection
    /// </summary>
    ISubject<ITransactionMessage<IEnumerable<T>>> CollectionStream { get; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class IndexCollection<T> : IIndexCollection<T> where T : IBaseModel
  {
    /// <summary>
    /// Count
    /// </summary>
    public virtual int Count => Items?.Count ?? 0;

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
    public virtual ISubject<ITransactionMessage<IEnumerable<T>>> CollectionStream { get; protected set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public IndexCollection()
    {
      Items = new List<T>();
      ItemStream = new Subject<ITransactionMessage<T>>();
      CollectionStream = new Subject<ITransactionMessage<IEnumerable<T>>>();
    }

    /// <summary>
    /// Get item by index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public virtual T this[int index]
    {
      get => Items[index];
      set
      {
        var item = value;

        var itemMessage = new TransactionMessage<T>
        {
          Next = item,
          Action = ActionEnum.Update
        };

        var collectionMessage = new TransactionMessage<IEnumerable<T>>
        {
          Next = Items,
          Action = ActionEnum.Update
        };

        if (Items.Any())
        {
          itemMessage.Previous = Items[index];
        }

        Items[index] = item;
        ItemStream.OnNext(itemMessage);
        CollectionStream.OnNext(collectionMessage);
      }
    }

    /// <summary>
    /// Add to the collection
    /// </summary>
    /// <param name="items"></param>
    public virtual void Add(params T[] items)
    {
      foreach (var dataItem in items)
      {
        var item = dataItem;

        var itemMessage = new TransactionMessage<T>
        {
          Next = item,
          Action = ActionEnum.Create
        };

        if (Items.Count > 0)
        {
          itemMessage.Previous = Items[Items.Count - 1];
        }

        Items.Add(item);
        ItemStream.OnNext(itemMessage);
      }

      var collectionMessage = new TransactionMessage<IEnumerable<T>>
      {
        Next = Items,
        Action = ActionEnum.Create
      };

      CollectionStream.OnNext(collectionMessage);
    }

    /// <summary>
    /// Remove from the collection
    /// </summary>
    /// <param name="item"></param>
    public virtual void Remove(T item)
    {
      var itemMessage = new TransactionMessage<T>
      {
        Previous = item,
        Action = ActionEnum.Delete
      };

      var collectionMessage = new TransactionMessage<IEnumerable<T>>
      {
        Next = Items,
        Action = ActionEnum.Delete
      };

      Items.Remove(item);
      ItemStream.OnNext(itemMessage);
      CollectionStream.OnNext(collectionMessage);
    }

    /// <summary>
    /// Update item in the collection
    /// </summary>
    /// <param name="item"></param>
    public virtual void Update(T item)
    {
      var itemMessage = new TransactionMessage<T>
      {
        Next = item,
        Action = ActionEnum.Update
      };

      var collectionMessage = new TransactionMessage<IEnumerable<T>>
      {
        Next = Items,
        Action = ActionEnum.Update
      };

      ItemStream.OnNext(itemMessage);
      CollectionStream.OnNext(collectionMessage);
    }

    /// <summary>
    /// Clear collection
    /// </summary>
    public virtual void Clear()
    {
      Items.Clear();
    }

    /// <summary>
    /// Get enumerator
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerator<T> GetEnumerator()
    {
      return Items.GetEnumerator();
    }

    /// <summary>
    /// Get enumerator
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return Items.GetEnumerator();
    }
  }
}
