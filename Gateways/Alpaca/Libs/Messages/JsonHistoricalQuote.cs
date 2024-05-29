using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonHistoricalQuote
{
  [JsonPropertyName("t")]
  public DateTime? TimestampUtc { get; set; }

  [JsonPropertyName("ax")]
  public string AskExchange { get; set; }

  [JsonPropertyName("ap")]
  public decimal? AskPrice { get; set; }

  [JsonPropertyName("as")]
  public decimal? AskSize { get; set; }

  [JsonPropertyName("bx")]
  public string BidExchange { get; set; }

  [JsonPropertyName("bp")]
  public decimal? BidPrice { get; set; }

  [JsonPropertyName("bs")]
  public decimal? BidSize { get; set; }

  [JsonPropertyName("c")]
  public List<string> ConditionsList { get; set; } = [];

  [JsonPropertyName("z")]
  public string Tape { get; set; }

  [JsonPropertyName("S")]
  public string Symbol { get; set; }
}
