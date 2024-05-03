using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonHistoricalTrade
{
  [JsonPropertyName("t")]
  public DateTime? TimestampUtc { get; set; }

  [JsonPropertyName("x")]
  public string Exchange { get; set; }

  [JsonPropertyName("p")]
  public double? Price { get; set; }

  [JsonPropertyName("s")]
  public double? Size { get; set; }

  [JsonPropertyName("i")]
  public double? TradeId { get; set; }

  [JsonPropertyName("z")]
  public string Tape { get; set; }

  [JsonPropertyName("u")]
  public string Update { get; set; }

  [JsonPropertyName("c")]
  public List<string> ConditionsList { get; set; } = [];

  [JsonPropertyName("tks")]
  public string TakerSide { get; set; }

  [JsonIgnore]
  public string Symbol { get; set; }
}
