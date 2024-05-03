using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonAuthResponse
{
  [JsonPropertyName("status")]
  public string Status { get; set; }

  [JsonPropertyName("message")]
  public string Message { get; set; }
}
