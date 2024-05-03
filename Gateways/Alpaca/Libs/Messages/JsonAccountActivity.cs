using System;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonAccountActivity
{
  [JsonPropertyName("activity_type")]
  public string ActivityType { get; set; }

  [JsonPropertyName("id")]
  public string ActivityId { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonIgnore]
  public DateOnly? ActivityDate { get; set; }

  [JsonPropertyName("net_amount")]
  public double? NetAmount { get; set; }

  [JsonPropertyName("per_share_amount")]
  public double? PerShareAmount { get; set; }

  [JsonPropertyName("qty")]
  public double? Quantity { get; set; }

  [JsonPropertyName("cum_qty")]
  public double? CumulativeQuantity { get; set; }

  [JsonPropertyName("leaves_qty")]
  public double? LeavesQuantity { get; set; }

  [JsonPropertyName("price")]
  public double? Price { get; set; }

  [JsonPropertyName("side")]
  public string Side { get; set; }

  [JsonPropertyName("type")]
  public string Type { get; set; }

  [JsonPropertyName("date")]
  public DateTime? ActivityDateTime { get; set; }

  [JsonIgnore]
  public DateTime? ActivityDateTimeUtc { get; set; }

  [JsonPropertyName("transaction_time")]
  public DateTime? TransactionTimeUtc { get; set; }

  [JsonIgnore]
  public string ActivityGuid { get; set; }

  [JsonPropertyName("order_id")]
  public string OrderId { get; set; }
}
