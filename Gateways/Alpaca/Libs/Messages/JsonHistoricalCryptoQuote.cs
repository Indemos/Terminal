using System;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonHistoricalCryptoQuote
{
  [JsonPropertyName("t")]
  public DateTime? TimestampUtc { get; set; }

  [JsonPropertyName("x")]
  public string AskExchange { get; set; }

  [JsonPropertyName("ap")]
  public double? AskPrice { get; set; }

  [JsonPropertyName("as")]
  public double? AskSize { get; set; }

  [JsonPropertyName("bp")]
  public double? BidPrice { get; set; }

  [JsonPropertyName("bs")]
  public double? BidSize { get; set; }

  [JsonIgnore]
  public string Symbol { get; set; }
}
