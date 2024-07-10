using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class QuoteMessage
{
  [JsonPropertyName("t")]
  public DateTime? TimestampUtc { get; set; }

  [JsonPropertyName("x")]
  public string Exchange { get; set; }

  [JsonPropertyName("ax")]
  public string AskExchange { get; set; }

  [JsonPropertyName("ap")]
  public double? AskPrice { get; set; }

  [JsonPropertyName("as")]
  public double? AskSize { get; set; }

  [JsonPropertyName("bx")]
  public string BidExchange { get; set; }

  [JsonPropertyName("bp")]
  public double? BidPrice { get; set; }

  [JsonPropertyName("bs")]
  public double? BidSize { get; set; }

  [JsonPropertyName("c")]
  public List<string> ConditionsList { get; set; } = [];

  [JsonPropertyName("z")]
  public string Tape { get; set; }

  [JsonPropertyName("S")]
  public string Symbol { get; set; }
}
