using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonMarketMover
{
  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("price")]
  public double? Price { get; set; }

  [JsonPropertyName("change")]
  public double? Change { get; set; }

  [JsonPropertyName("percent_change")]
  public double? PercentChange { get; set; }
}
