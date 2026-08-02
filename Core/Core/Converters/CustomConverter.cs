using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Converters
{
  public class CustomConverter<T> : JsonConverter<T>
  {
    public override bool HandleNull => true;

    public override T Read(ref Utf8JsonReader reader, Type dataType, JsonSerializerOptions options)
    {
      try
      {
        if (reader.TokenType is JsonTokenType.Null)
        {
          return default;
        }

        var value = $"{JsonDocument.ParseValue(ref reader).RootElement}";

        if (string.Equals("NULL", value, StringComparison.OrdinalIgnoreCase))
        {
          return default;
        }

        return (T)Convert.ChangeType(value, dataType);
      }
      catch (Exception)
      {
        return default;
      }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
      if (value is null)
      {
        return;
      }

      writer.WriteStringValue($"{value}");
    }
  }
}
