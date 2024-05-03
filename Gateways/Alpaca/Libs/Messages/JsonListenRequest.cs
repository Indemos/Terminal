using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonListenRequest
{
  public class JsonData
  {
    [JsonPropertyName("streams")]
    public List<string> Streams { get; set; } = [];
  }

  [JsonPropertyName("action")]
  public string Action { get; set; }

  [JsonPropertyName("data")]
  public JsonData Data { get; set; }
}
