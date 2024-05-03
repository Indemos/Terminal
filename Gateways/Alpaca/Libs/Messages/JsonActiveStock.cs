using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonActiveStock
{
  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("volume")]
  public double? Volume { get; set; }

  [JsonPropertyName("trade_count")]
  public double? TradeCount { get; set; }
}
