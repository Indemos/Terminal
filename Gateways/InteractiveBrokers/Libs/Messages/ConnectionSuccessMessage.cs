using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class ConnectionSuccessMessage
{
  [JsonPropertyName("msg")]
  public string Status { get; set; }
}
