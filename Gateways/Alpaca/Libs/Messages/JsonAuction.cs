using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonAuction
{
  [JsonPropertyName("d")]
  internal DateTime? DateTime { get; set; }

  [JsonPropertyName("o")]
  internal List<JsonAuctionEntry> OpeningsList { get; set; } = [];

  [JsonPropertyName("c")]
  internal List<JsonAuctionEntry> ClosingsList { get; set; } = [];

  [JsonIgnore]
  public string Symbol { get; set; }

  [JsonIgnore]
  public List<JsonAuctionEntry> Openings { get; set; } = [];

  [JsonIgnore]
  public List<JsonAuctionEntry> Closings { get; set; } = [];
}
