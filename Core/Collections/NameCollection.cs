using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Core.EnumSpace;
using Core.ManagerSpace;
using Core.MessageSpace;
using Core.ModelSpace;

namespace Core.CollectionSpace
{
  /// <summary>
  /// Name based collection
  /// </summary>
  /// <typeparam name="TKey"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public interface INameCollection<TKey, TValue> : IDictionary<TKey, TValue> where TValue : IBaseModel
  {
    /// <summary>
    /// Add item using specific index
    /// </summary>
    /// <param name="dataItem"></param>
    void Add(TValue dataItem);

    /// <summary>
    /// Observable item changes
    /// </summary>
    IObservable<ITransactionMessage<TValue>> ObservableItem { get; }

    /// <summary>
    /// Observable items changes
    /// </summary>
    IObservable<ITransactionMessage<IDictionary<TKey, TValue>>> ObservableItems { get; }
  }

  /// <summary>
  /// Name based collection
  /// </summary>
  /// <typeparam name="TKey"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public class NameCollection<TKey, TValue> : INameCollection<TKey, TValue> where TValue : IBaseModel
  {
    /// <summary>
    /// Internal collection
    /// </summary>
    protected IDictionary<TKey, TValue> _items = new Dictionary<TKey, TValue>();

    /// <summary>
    /// Observable item changes
    /// </summary>
    protected ISubject<ITransactionMessage<TValue>> _observableItem = new Subject<ITransactionMessage<TValue>>();

    /// <summary>
    /// Observable items changes
    /// </summary>
    protected ISubject<ITransactionMessage<IDictionary<TKey, TValue>>> _observableItems = new Subject<ITransactionMessage<IDictionary<TKey, TValue>>>();

    /// <summary>
    /// Standard dictionary implementation
    /// </summary>
    public virtual int Count => _items.Count;
    public virtual bool IsReadOnly => _items.IsReadOnly;
    public virtual bool Contains(KeyValuePair<TKey, TValue> item) => _items.Contains(item);
    public virtual bool ContainsKey(TKey key) => _items.ContainsKey(key);
    public virtual void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public virtual bool TryGetValue(TKey key, out TValue value) => _items.TryGetValue(key, out value);
    public virtual ICollection<TKey> Keys => _items.Keys;
    public virtual ICollection<TValue> Values => _items.Values;
    public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _items.GetEnumerator();
    public virtual IObservable<ITransactionMessage<TValue>> ObservableItem => _observableItem.DistinctUntilChanged();
    public virtual IObservable<ITransactionMessage<IDictionary<TKey, TValue>>> ObservableItems => _observableItems.DistinctUntilChanged();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    /// <summary>
    /// Get item by index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public virtual TValue this[TKey index]
    {
      get => TryGetValue(index, out TValue item) ? item : default;
      set => Add(index, value, ActionEnum.Update);
    }

    /// <summary>
    /// Add a pair to the dictionary
    /// </summary>
    /// <param name="item"></param>
    public virtual void Add(KeyValuePair<TKey, TValue> item)
    {
      Add(item.Key, item.Value, ActionEnum.Create);
    }

    /// <summary>
    /// Add item using specific index
    /// </summary>
    /// <param name="index"></param>
    /// <param name="dataItem"></param>
    public virtual void Add(TKey index, TValue dataItem)
    {
      Add(index, dataItem, ActionEnum.Create);
    }

    /// <summary>
    /// Add item using specific index
    /// </summary>
    /// <param name="dataItem"></param>
    public virtual void Add(TValue dataItem)
    {
      Add((TKey)(object)dataItem.Name, dataItem, ActionEnum.Create);
    }

    /// <summary>
    /// Add item using specific index
    /// </summary>
    /// <param name="index"></param>
    /// <param name="dataItem"></param>
    public virtual void Add(TKey index, TValue dataItem, ActionEnum action)
    {
      var item = dataItem;
      var itemMessage = new TransactionMessage<TValue>
      {
        Next = item,
        Previous = _items.Any() ? _items.Last().Value : default,
        Action = action
      };

      var itemsMessage = new TransactionMessage<IDictionary<TKey, TValue>>
      {
        Next = _items,
        Action = action
      };

      _items[index] = item;
      _observableItem.OnNext(itemMessage);
      _observableItems.OnNext(itemsMessage);
    }

    /// <summary>
    /// Clear collection
    /// </summary>
    public virtual void Clear()
    {
      var itemMessage = new TransactionMessage<TValue>
      {
        Action = ActionEnum.Clear
      };

      var itemsMessage = new TransactionMessage<IDictionary<TKey, TValue>>
      {
        Action = ActionEnum.Clear
      };

      _items.Clear();
      _observableItem.OnNext(itemMessage);
      _observableItems.OnNext(itemsMessage);
    }

    /// <summary>
    /// Remove item by index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public virtual bool Remove(TKey index)
    {
      var response = false;
      var item = ConversionManager.Value(_items, index);

      if (item == null || _items.ContainsKey(index) == false)
      {
        return response;
      }

      var itemMessage = new TransactionMessage<TValue>
      {
        Previous = _items[index],
        Action = ActionEnum.Delete
      };

      var itemsMessage = new TransactionMessage<IDictionary<TKey, TValue>>
      {
        Next = _items,
        Action = ActionEnum.Delete
      };

      response = _items.Remove(index);

      _observableItem.OnNext(itemMessage);
      _observableItems.OnNext(itemsMessage);

      return response;
    }

    /// <summary>
    /// Remove a pair from the collection
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public virtual bool Remove(KeyValuePair<TKey, TValue> item)
    {
      return Remove(item.Key);
    }
  }
}
