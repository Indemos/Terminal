using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonRealTimeBar
{
  [JsonPropertyName("o")]
  public double? Open { get; set; }

  [JsonPropertyName("h")]
  public double? High { get; set; }

  [JsonPropertyName("l")]
  public double? Low { get; set; }

  [JsonPropertyName("c")]
  public double? Close { get; set; }

  [JsonPropertyName("v")]
  public double? Volume { get; set; }

  [JsonPropertyName("vw")]
  public double? Vwap { get; set; }

  [JsonPropertyName("n")]
  public int TradeCount { get; set; }
}
