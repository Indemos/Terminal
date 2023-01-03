using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Terminal.Core.EnumSpace;
using Terminal.Core.MessageSpace;
using Terminal.Core.ModelSpace;

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
    /// Observable item changes
    /// </summary>
    ISubject<ITransactionMessage<TValue>> ItemStream { get; }

    /// <summary>
    /// Observable items changes
    /// </summary>
    ISubject<ITransactionMessage<IDictionary<TKey, TValue>>> ItemsStream { get; }
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
    public virtual ISubject<ITransactionMessage<IDictionary<TKey, TValue>>> ItemsStream { get; protected set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public NameCollection()
    {
      Items = new ConcurrentDictionary<TKey, TValue>();
      ItemStream = new Subject<ITransactionMessage<TValue>>();
      ItemsStream = new Subject<ITransactionMessage<IDictionary<TKey, TValue>>>();
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
    /// <param name="index"></param>
    /// <param name="item"></param>
    public virtual void Add(TKey index, TValue item, ActionEnum action)
    {
      var previous = this[index];

      Items[index] = item;

      SendItemMessage(item, previous, action);
      SendItemsMessage(action);
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
    /// Remove item by index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public virtual bool Remove(TKey index)
    {
      if (Items.ContainsKey(index) is false)
      {
        return false;
      }

      var previous = Items[index];
      var response = Items.Remove(index);

      SendItemMessage(default, previous, ActionEnum.Delete);
      SendItemsMessage(ActionEnum.Delete);

      return response;
    }

    /// <summary>
    /// Remove a pair from the collection
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public virtual bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

    /// <summary>
    /// Send item message
    /// </summary>
    /// <param name="next"></param>
    /// <param name="previous"></param>
    /// <param name="action"></param>
    protected virtual void SendItemMessage(TValue next, TValue previous, ActionEnum action)
    {
      var itemMessage = new TransactionMessage<TValue>
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
      var collectionMessage = new TransactionMessage<IDictionary<TKey, TValue>>
      {
        Next = Items,
        Action = action
      };

      ItemsStream.OnNext(collectionMessage);
    }
  }
}
