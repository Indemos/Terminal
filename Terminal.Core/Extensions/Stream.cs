using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Terminal.Core.ExtensionSpace
{
  public static class StreamExtensions
  {
    public static ValueTask<T> DeserializeAsync<T>(this Stream input, JsonSerializerOptions options = null)
    {
      return JsonSerializer.DeserializeAsync<T>(input, options);
    }
  }
}
