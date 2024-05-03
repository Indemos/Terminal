using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonLatestBar
{
  [JsonPropertyName("bar")]
  public JsonHistoricalBar Nested { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
