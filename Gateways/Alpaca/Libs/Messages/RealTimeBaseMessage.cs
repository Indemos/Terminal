using System;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class RealTimeBaseMessage
{
  [JsonPropertyName("T")]
  public string Channel { get; set; }

  [JsonPropertyName("S")]
  public string Symbol { get; set; }

  [JsonPropertyName("t")]
  public DateTime? TimestampUtc { get; set; }
}
