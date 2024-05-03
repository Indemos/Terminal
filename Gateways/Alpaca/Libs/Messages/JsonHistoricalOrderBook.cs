using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonHistoricalOrderBook
{
  [JsonPropertyName("t")]
  public DateTime? TimestampUtc { get; set; }

  [JsonPropertyName("b")]
  public List<JsonOrderBookEntry> BidsList { get; set; } = [];

  [JsonPropertyName("a")]
  public List<JsonOrderBookEntry> AsksList { get; set; } = [];

  [JsonIgnore]
  public string Symbol { get; set; }

  [JsonIgnore]
  public List<JsonOrderBookEntry> Bids { get; set; } = [];

  [JsonIgnore]
  public List<JsonOrderBookEntry> Asks { get; set; } = [];
}
