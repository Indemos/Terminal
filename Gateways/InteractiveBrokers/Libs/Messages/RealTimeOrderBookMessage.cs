using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class RealTimeOrderBookMessage
{
  [JsonPropertyName("x")]
  public string Exchange { get; set; }

  [JsonPropertyName("b")]
  public List<OrderBookEntryMessage> BidsList { get; set; } = [];

  [JsonPropertyName("a")]
  public List<OrderBookEntryMessage> AsksList { get; set; } = [];

  [JsonPropertyName("r")]
  public bool IsReset { get; set; }

  [JsonIgnore]
  public List<OrderBookEntryMessage> Bids { get; set; } = [];

  [JsonIgnore]
  public List<OrderBookEntryMessage> Asks { get; set; } = [];
}
