using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonLatestTrade
{
  [JsonPropertyName("trade")]
  public JsonHistoricalTrade Nested { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
