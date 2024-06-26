using System;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class TradeUpdateMessage
{
  [JsonPropertyName("event")]
  public string Event { get; set; }

  [JsonPropertyName("execution_id")]
  public string ExecutionId { get; set; }

  [JsonPropertyName("price")]
  public double? Price { get; set; }

  [JsonPropertyName("position_qty")]
  public double? PositionQuantity { get; set; }

  [JsonPropertyName("qty")]
  public double? TradeQuantity { get; set; }

  [JsonPropertyName("timestamp")]
  public DateTime? TimestampUtc { get; set; }

  [JsonPropertyName("order")]
  public OrderMessage JsonOrder { get; set; } = new();
}
