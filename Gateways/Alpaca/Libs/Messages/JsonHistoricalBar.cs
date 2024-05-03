using System;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonHistoricalBar
{
  [JsonIgnore]
  public string Symbol { get; set; }

  [JsonPropertyName("o")]
  public double? Open { get; set; }

  [JsonPropertyName("c")]
  public double? Close { get; set; }

  [JsonPropertyName("l")]
  public double? Low { get; set; }

  [JsonPropertyName("h")]
  public double? High { get; set; }

  [JsonPropertyName("v")]
  public double? Volume { get; set; }

  [JsonPropertyName("t")]
  public DateTime? TimeUtc { get; set; }

  [JsonPropertyName("vw")]
  public double? Vwap { get; set; }

  [JsonPropertyName("n")]
  public int TradeCount { get; set; }
}
