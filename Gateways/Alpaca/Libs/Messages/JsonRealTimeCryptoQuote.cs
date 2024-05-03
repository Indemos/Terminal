using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonRealTimeCryptoQuote
{
  [JsonPropertyName("x")]
  public string BidExchange { get; set; }

  [JsonPropertyName("bp")]
  public double? BidPrice { get; set; }

  [JsonPropertyName("ap")]
  public double? AskPrice { get; set; }

  [JsonPropertyName("bs")]
  public double? BidSize { get; set; }

  [JsonPropertyName("as")]
  public double? AskSize { get; set; }

  [JsonPropertyName("c")]
  public List<string> ConditionsList { get; set; } = [];

  [JsonPropertyName("z")]
  public string Tape { get; set; }
}
