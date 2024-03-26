using System.Collections.Specialized;
using System;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Terminal.Core.Extensions
{
  public static class StringExtensions
  {
    public static int? ToInt(this string input)
    {
      return int.TryParse(input, out int o) ? o : null;
    }

    public static double? ToDouble(this string input)
    {
      return double.TryParse(input, out double o) ? o : null;
    }

    public static decimal? ToDecimal(this string input)
    {
      return decimal.TryParse(input, out decimal o) ? o : null;
    }

    public static byte[] ToBytes(this string input)
    {
      return Encoding.UTF8.GetBytes(input);
    }

    public static T Deserialize<T>(this string input, JsonSerializerOptions options = null)
    {
      return JsonSerializer.Deserialize<T>(input, options);
    }

    public static NameValueCollection ToPairs(this string input)
    {
      return HttpUtility.ParseQueryString(new Uri(input).Query);
    }
  }
}
