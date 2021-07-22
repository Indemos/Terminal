using Core.EnumSpace;
using Core.MessageSpace;
using Core.ModelSpace;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

namespace Core.CollectionSpace
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
    IObservable<ITransactionMessage<T>> ItemStream { get; }

    /// <summary>
    /// Observable collection
    /// </summary>
    IObservable<ITransactionMessage<IEnumerable<T>>> CollectionStream { get; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class IndexCollection<T> : IIndexCollection<T> where T : IBaseModel
  {
    /// <summary>
    /// Streams
    /// </summary>
    protected IList<T> _items = new List<T>();
    protected ISubject<ITransactionMessage<T>> _itemStream = new Subject<ITransactionMessage<T>>();
    protected ISubject<ITransactionMessage<IEnumerable<T>>> _collectionSrteam = new Subject<ITransactionMessage<IEnumerable<T>>>();

    /// <summary>
    /// Count
    /// </summary>
    public virtual int Count => _items.Count;

    /// <summary>
    /// Observable item
    /// </summary>
    public virtual IObservable<ITransactionMessage<T>> ItemStream => _itemStream;

    /// <summary>
    /// Observable collection
    /// </summary>
    public virtual IObservable<ITransactionMessage<IEnumerable<T>>> CollectionStream => _collectionSrteam;

    /// <summary>
    /// Get item by index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public virtual T this[int index]
    {
      get => _items[index];
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
          Next = _items,
          Action = ActionEnum.Update
        };

        if (_items.Any())
        {
          itemMessage.Previous = _items[index];
        }

        _items[index] = item;
        _itemStream.OnNext(itemMessage);
        _collectionSrteam.OnNext(collectionMessage);
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

        if (_items.Count > 0)
        {
          itemMessage.Previous = _items[_items.Count - 1];
        }

        _items.Add(item);
        _itemStream.OnNext(itemMessage);
      }

      var collectionMessage = new TransactionMessage<IEnumerable<T>>
      {
        Next = _items,
        Action = ActionEnum.Create
      };

      _collectionSrteam.OnNext(collectionMessage);
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
        Next = _items,
        Action = ActionEnum.Delete
      };

      _items.Remove(item);
      _itemStream.OnNext(itemMessage);
      _collectionSrteam.OnNext(collectionMessage);
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
        Next = _items,
        Action = ActionEnum.Update
      };

      _itemStream.OnNext(itemMessage);
      _collectionSrteam.OnNext(collectionMessage);
    }

    /// <summary>
    /// Clear collection
    /// </summary>
    public virtual void Clear()
    {
      _items.Clear();
    }

    /// <summary>
    /// Get enumerator
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerator<T> GetEnumerator()
    {
      return _items.GetEnumerator();
    }

    /// <summary>
    /// Get enumerator
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return _items.GetEnumerator();
    }
  }
}
