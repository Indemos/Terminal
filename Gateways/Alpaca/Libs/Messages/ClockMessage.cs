using System;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class ClockMessage
{
  [JsonPropertyName("timestamp")]
  public DateTime? TimestampUtc { get; set; }

  [JsonPropertyName("is_open")]
  public bool IsOpen { get; set; }

  [JsonPropertyName("next_open")]
  public DateTime? NextOpenUtc { get; set; }

  [JsonPropertyName("next_close")]
  public DateTime? NextCloseUtc { get; set; }
}
