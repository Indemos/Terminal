using System.Collections.Generic;
using System.Web;

namespace Terminal.Core.Extensions
{
  public static class DictionaryExtensions
  {
    public static string ToQuery<K, V>(this IDictionary<K, V> input)
    {
      var inputs = HttpUtility.ParseQueryString(string.Empty);

      if (input is not null)
      {
        foreach (var item in input)
        {
          inputs[$"{item.Key}"] = $"{item.Value}";
        }
      }

      return $"{inputs}";
    }
  }
}
