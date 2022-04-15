using Terminal.Core.EnumSpace;
using Terminal.Core.MessageSpace;
using Terminal.Core.ModelSpace;
using Terminal.Core.ServiceSpace;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Terminal.Core.CollectionSpace
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
    ISubject<ITransactionMessage<TValue>> ItemStream { get; }

    /// <summary>
    /// Observable items changes
    /// </summary>
    ISubject<ITransactionMessage<IDictionary<TKey, TValue>>> CollectionStream { get; }
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
    public virtual IDictionary<TKey, TValue> Items { get; protected set; }

    /// <summary>
    /// Observable item changes
    /// </summary>
    public virtual ISubject<ITransactionMessage<TValue>> ItemStream { get; protected set; }

    /// <summary>
    /// Observable items changes
    /// </summary>
    public virtual ISubject<ITransactionMessage<IDictionary<TKey, TValue>>> CollectionStream { get; protected set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public NameCollection()
    {
      Items = new Dictionary<TKey, TValue>();
      ItemStream = new Subject<ITransactionMessage<TValue>>();
      CollectionStream = new Subject<ITransactionMessage<IDictionary<TKey, TValue>>>();
    }

    /// <summary>
    /// Standard dictionary implementation
    /// </summary>
    public virtual int Count => Items.Count;
    public virtual bool IsReadOnly => Items.IsReadOnly;
    public virtual bool Contains(KeyValuePair<TKey, TValue> item) => Items.Contains(item);
    public virtual bool ContainsKey(TKey key) => Items.ContainsKey(key);
    public virtual void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => Items.CopyTo(array, arrayIndex);
    public virtual bool TryGetValue(TKey key, out TValue value) => Items.TryGetValue(key, out value);
    public virtual ICollection<TKey> Keys => Items.Keys;
    public virtual ICollection<TValue> Values => Items.Values;
    public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();

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
        Previous = Items.Any() ? Items.Last().Value : default,
        Action = action
      };

      var itemsMessage = new TransactionMessage<IDictionary<TKey, TValue>>
      {
        Next = Items,
        Action = action
      };

      Items[index] = item;
      ItemStream.OnNext(itemMessage);
      CollectionStream.OnNext(itemsMessage);
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

      Items.Clear();
      ItemStream.OnNext(itemMessage);
      CollectionStream.OnNext(itemsMessage);
    }

    /// <summary>
    /// Remove item by index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public virtual bool Remove(TKey index)
    {
      var response = false;

      if (Items.TryGetValue(index, out TValue item) is false || item is null)
      {
        return response;
      }

      var itemMessage = new TransactionMessage<TValue>
      {
        Previous = Items[index],
        Action = ActionEnum.Delete
      };

      var itemsMessage = new TransactionMessage<IDictionary<TKey, TValue>>
      {
        Next = Items,
        Action = ActionEnum.Delete
      };

      response = Items.Remove(index);

      ItemStream.OnNext(itemMessage);
      CollectionStream.OnNext(itemsMessage);

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
