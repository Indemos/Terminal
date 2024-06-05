using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class HistoricalOrderBookMessage
{
  [JsonPropertyName("t")]
  public DateTime? TimestampUtc { get; set; }

  [JsonPropertyName("b")]
  public List<OrderBookEntryMessage> BidsList { get; set; } = [];

  [JsonPropertyName("a")]
  public List<OrderBookEntryMessage> AsksList { get; set; } = [];

  [JsonIgnore]
  public string Symbol { get; set; }

  [JsonIgnore]
  public List<OrderBookEntryMessage> Bids { get; set; } = [];

  [JsonIgnore]
  public List<OrderBookEntryMessage> Asks { get; set; } = [];
}
