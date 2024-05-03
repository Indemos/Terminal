using System;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonOptionQuote
{
  [JsonPropertyName("t")]
  public DateTime? TimestampUtc { get; set; }

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
}
