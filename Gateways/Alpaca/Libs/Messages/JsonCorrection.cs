using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonCorrection
{
  [JsonPropertyName("x")]
  public string Exchange { get; set; }

  [JsonPropertyName("z")]
  public string Tape { get; set; }

  [JsonPropertyName("u")]
  public string Update { get; set; }

  [JsonPropertyName("oi")]
  public double? TradeId { get; set; }

  [JsonPropertyName("op")]
  public double? Price { get; set; }

  [JsonPropertyName("os")]
  public double? Size { get; set; }

  [JsonPropertyName("oc")]
  public List<string> ConditionsList { get; set; } = [];

  [JsonPropertyName("ci")]
  public double? CorrectedTradeId { get; set; }

  [JsonPropertyName("cp")]
  public double? CorrectedPrice { get; set; }

  [JsonPropertyName("cs")]
  public double? CorrectedSize { get; set; }

  [JsonPropertyName("tks")]
  public string TakerSide { get; set; }

  [JsonIgnore]
  public JsonRealTimeTrade CorrectedTrade { get; set; }
}
