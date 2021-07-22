using Core.CollectionSpace;
using Core.EnumSpace;
using Core.ModelSpace;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.ManagerSpace
{
  /// <summary>
  /// JSON serialization options
  /// </summary>
  public class ConversionOptions : JsonSerializerSettings
  {
  }

  /// <summary>
  /// Helper class for the most common conversion operations
  /// </summary>
  public class ConversionManager
  {
    /// <summary>
    /// Encoder dictionary
    /// </summary>
    private static readonly Dictionary<long, char> _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
      .Select((v, i) => new { v, i })
      .ToDictionary(o => (long)o.i + 1, o => o.v);

    /// <summary>
    /// Decoder dictionary
    /// </summary>
    private static readonly Dictionary<char, long> _numbers = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
      .Select((v, i) => new { v, i })
      .ToDictionary(o => o.v, o => (long)o.i + 1);

    /// <summary>
    /// Create instance
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T Instance<T>() where T : new()
    {
      return new T();
    }

    /// <summary>
    /// Compare double values
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="epsilon"></param>
    /// <returns></returns>
    public static bool Equals(double? v1, double? v2, double epsilon = double.Epsilon)
    {
      return Math.Abs(v1.Value - v2.Value) < epsilon;
    }

    /// <summary>
    /// Parse T
    /// </summary>
    /// <param name="content"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T Enum<T>(dynamic content, T value = default)
    {
      if (content == null)
      {
        return value;
      }

      try
      {
        var cache = InstanceManager<CacheService<dynamic, T>>.Instance.Cache;

        if (cache.TryGetValue(content, out T response))
        {
          return response;
        }

        value = cache[content] = (T)System.Enum.Parse(typeof(T), content, true);
      }
      catch (Exception e)
      {
        InstanceManager<LogService>.Instance.Log.Error(e.Message);
      }

      return value;
    }

    /// <summary>
    /// Parse T
    /// </summary>
    /// <param name="content"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T Value<T>(dynamic content, T value = default)
    {
      if (content == null)
      {
        return value;
      }

      try
      {
        return Convert.ChangeType(content, typeof(T));
      }
      catch (Exception e)
      {
        InstanceManager<LogService>.Instance.Log.Error(e.Message);
      }

      return value;
    }

    /// <summary>
    /// Extract T from collection
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T Value<T>(dynamic collection, dynamic name, T value = default)
    {
      if (collection == null)
      {
        return value;
      }

      try
      {
        return Convert.ChangeType(collection[name], typeof(T));
      }
      catch (Exception e)
      {
        InstanceManager<LogService>.Instance.Log.Error(e.Message);
      }

      return value;
    }

    /// <summary>
    /// Convert collection to IEnumerable<T>
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static List<T> Values<T>(dynamic collection, List<T> value = default)
    {
      if (collection == null)
      {
        return value;
      }

      try
      {
        var items = new List<T>();

        foreach (dynamic item in collection)
        {
          items.Add(Convert.ChangeType(item, typeof(T)));
        }

        return items;
      }
      catch (Exception e)
      {
        InstanceManager<LogService>.Instance.Log.Error(e.Message);
      }

      return value;
    }

    /// <summary>
    /// Extract IEnumerable<T> from collection
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static List<T> Values<T>(dynamic collection, dynamic name, List<T> value = default)
    {
      try
      {
        return Values<T>(collection[name]);
      }
      catch (Exception e)
      {
        InstanceManager<LogService>.Instance.Log.Error(e.Message);
      }

      return value;
    }

    /// <summary>
    /// Convert string to number 
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    public static long Encode(string content)
    {
      var number = 0L;

      for (var i = 0; i < content.Length; i++)
      {
        number *= _numbers.Count;
        number += _numbers[char.ToUpper(content[i])];
      }

      return number;
    }

    /// <summary>
    /// Convert number to string
    /// </summary>
    /// <param name="number"></param>
    /// <returns></returns>
    public static string Decode(long number)
    {
      var response = string.Empty;

      while (number > 0)
      {
        response = _chars[number % _chars.Count] + response;
        number /= _chars.Count;
      }

      return response;
    }

    /// <summary>
    /// Round value to specified interval
    /// </summary>
    /// <param name="dateTime"></param>
    /// <param name="span"></param>
    /// <returns></returns>
    public static DateTime? Round(DateTime? dateTime, TimeSpan? span)
    {
      if (dateTime == null || span == null)
      {
        return null;
      }

      return new DateTime(dateTime.Value.Ticks - (dateTime.Value.Ticks % span.Value.Ticks), dateTime.Value.Kind);
    }

    /// <summary>
    /// Serialize to JSON
    /// </summary>
    /// <param name="data"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static string Serialize(object data, ConversionOptions options = null)
    {
      return JsonConvert.SerializeObject(data, options);
    }

    /// <summary>
    /// Deserialize from JSON
    /// </summary>
    /// <param name="data"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static T Deserialize<T>(string data, ConversionOptions options = null)
    {
      return JsonConvert.DeserializeObject<T>(data, options);
    }
  }
}
