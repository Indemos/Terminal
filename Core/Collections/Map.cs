using System.Collections.Concurrent;

namespace Terminal.Core.Collections
{
  public class Map<TKey, TValue> : ConcurrentDictionary<TKey, TValue> where TValue : new()
  {
    public new TValue this[TKey index]
    {
      get => TryGetValue(index, out var o) ? o : base[index] = new TValue();
      set => base[index] = value;
    }
  }
}
