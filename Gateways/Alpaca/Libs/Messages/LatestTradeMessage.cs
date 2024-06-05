using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class LatestTradeMessage
{
  [JsonPropertyName("trade")]
  public HistoricalTradeMessage Nested { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
