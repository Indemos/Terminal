using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonRealTimeOrderBook
{
  [JsonPropertyName("x")]
  public string Exchange { get; set; }

  [JsonPropertyName("b")]
  public List<JsonOrderBookEntry> BidsList { get; set; } = [];

  [JsonPropertyName("a")]
  public List<JsonOrderBookEntry> AsksList { get; set; } = [];

  [JsonPropertyName("r")]
  public bool IsReset { get; set; }

  [JsonIgnore]
  public List<JsonOrderBookEntry> Bids { get; set; } = [];

  [JsonIgnore]
  public List<JsonOrderBookEntry> Asks { get; set; } = [];
}
