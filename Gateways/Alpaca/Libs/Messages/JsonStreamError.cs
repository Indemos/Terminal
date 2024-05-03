using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonStreamError
{
  [JsonPropertyName("code")]
  public double? Code { get; set; }

  [JsonPropertyName("msg")]
  public string Message { get; set; }
}
