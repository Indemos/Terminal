using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class ConnectionSuccessMessage
{
  [JsonPropertyName("msg")]
  public string Status { get; set; }
}
