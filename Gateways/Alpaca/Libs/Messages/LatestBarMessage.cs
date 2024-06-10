using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class LatestBarMessage
{
  [JsonPropertyName("bar")]
  public HistoricalBarMessage Nested { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
