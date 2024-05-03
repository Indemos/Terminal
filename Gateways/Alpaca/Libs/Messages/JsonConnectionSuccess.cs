using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonConnectionSuccess
{
  [JsonPropertyName("msg")]
  public string Status { get; set; }
}
