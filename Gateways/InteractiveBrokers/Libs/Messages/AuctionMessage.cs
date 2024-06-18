using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class AuctionMessage
{
  [JsonPropertyName("d")]
  internal DateTime? DateTime { get; set; }

  [JsonPropertyName("o")]
  internal List<AuctionEntryMessage> OpeningsList { get; set; } = [];

  [JsonPropertyName("c")]
  internal List<AuctionEntryMessage> ClosingsList { get; set; } = [];

  [JsonIgnore]
  public string Symbol { get; set; }

  [JsonIgnore]
  public List<AuctionEntryMessage> Openings { get; set; } = [];

  [JsonIgnore]
  public List<AuctionEntryMessage> Closings { get; set; } = [];
}
