using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class LatestTradeMessage
{
  [JsonPropertyName("trade")]
  public HistoricalTradeMessage Nested { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
