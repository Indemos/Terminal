using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonAuthentication
{
  [JsonPropertyName("action")]
  public string Action { get; set; }

  [JsonPropertyName("key")]
  public string KeyId { get; set; }

  [JsonPropertyName("secret")]
  public string SecretKey { get; set; }
}
