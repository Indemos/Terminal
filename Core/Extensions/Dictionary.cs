using System.Collections.Generic;
using System.Web;

namespace Terminal.Core.Extensions
{
  public static class DictionaryExtensions
  {
    public static V Get<K, V>(this IDictionary<K, V> input, K index)
    {
      return input.TryGetValue(index, out var value) ? value : default;
    }
  }
}
