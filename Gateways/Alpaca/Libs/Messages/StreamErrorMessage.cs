using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class StreamErrorMessage
{
  [JsonPropertyName("code")]
  public double? Code { get; set; }

  [JsonPropertyName("msg")]
  public string Message { get; set; }
}
