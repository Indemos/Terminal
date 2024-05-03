using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonAuthRequest
{
  public class JsonData
  {
    [JsonPropertyName("key_id")]
    public string KeyId { get; set; }

    [JsonPropertyName("secret_key")]
    public string SecretKey { get; set; }

    [JsonPropertyName("oauth_token")]
    public string OAuthToken { get; set; }
  }

  [JsonPropertyName("action")]
  public string Action { get; set; }

  [JsonPropertyName("data")]
  public JsonData Data { get; set; }
}
