using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class ActiveStockMessage
{
  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("volume")]
  public double? Volume { get; set; }

  [JsonPropertyName("trade_count")]
  public double? TradeCount { get; set; }
}
