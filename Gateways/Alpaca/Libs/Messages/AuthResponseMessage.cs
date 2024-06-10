using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class AuthResponseMessage
{
  [JsonPropertyName("status")]
  public string Status { get; set; }

  [JsonPropertyName("message")]
  public string Message { get; set; }
}
