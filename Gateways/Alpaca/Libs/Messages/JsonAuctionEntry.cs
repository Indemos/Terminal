using System;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonAuctionEntry
{
  [JsonPropertyName("t")]
  public DateTime? TimestampUtc { get; set; }

  [JsonPropertyName("p")]
  public double? Price { get; set; }

  [JsonPropertyName("s")]
  public double? Size { get; set; }

  [JsonPropertyName("x")]
  public string Exchange { get; set; }

  [JsonPropertyName("c")]
  public string Condition { get; set; }
}
