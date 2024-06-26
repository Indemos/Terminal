using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class AuthenticationMessage
{
  [JsonPropertyName("action")]
  public string Action { get; set; }

  [JsonPropertyName("key")]
  public string KeyId { get; set; }

  [JsonPropertyName("secret")]
  public string SecretKey { get; set; }
}
