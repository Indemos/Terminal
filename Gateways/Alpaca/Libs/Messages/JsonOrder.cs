using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonOrder
{
  [JsonPropertyName("id")]
  public string OrderId { get; set; }

  [JsonPropertyName("client_order_id")]
  public string ClientOrderId { get; set; }

  [JsonPropertyName("created_at")]

  public DateTime? CreatedAtUtc { get; set; }

  [JsonPropertyName("updated_at")]

  public DateTime? UpdatedAtUtc { get; set; }

  [JsonPropertyName("submitted_at")]

  public DateTime? SubmittedAtUtc { get; set; }

  [JsonPropertyName("filled_at")]

  public DateTime? FilledAtUtc { get; set; }

  [JsonPropertyName("expired_at ")]

  public DateTime? ExpiredAtUtc { get; set; }

  [JsonPropertyName("canceled_at")]

  public DateTime? CancelledAtUtc { get; set; }

  [JsonPropertyName("failed_at")]

  public DateTime? FailedAtUtc { get; set; }

  [JsonPropertyName("replaced_at")]

  public DateTime? ReplacedAtUtc { get; set; }

  [JsonPropertyName("asset_id")]
  public string AssetId { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; } = string.Empty;

  [JsonPropertyName("asset_class")]
  public string AssetClass { get; set; }

  [JsonPropertyName("notional")]
  public double? Notional { get; set; }

  [JsonPropertyName("qty")]
  public double? Quantity { get; set; }

  [JsonPropertyName("filled_qty")]
  public double? FilledQuantity { get; set; }

  [JsonPropertyName("type")]
  public string OrderType { get; set; }

  [JsonPropertyName("order_class")]
  public string OrderClass { get; set; }

  [JsonPropertyName("side")]
  public string OrderSide { get; set; }

  [JsonPropertyName("time_in_force")]
  public string TimeInForce { get; set; }

  [JsonPropertyName("limit_price")]
  public double? LimitPrice { get; set; }

  [JsonPropertyName("stop_price")]
  public double? StopPrice { get; set; }

  [JsonPropertyName("trail_price")]
  public double? TrailOffsetInDollars { get; set; }

  [JsonPropertyName("trail_percent")]
  public double? TrailOffsetInPercent { get; set; }

  [JsonPropertyName("hwm")]
  public double? HighWaterMark { get; set; }

  [JsonPropertyName("filled_avg_price")]
  public double? AverageFillPrice { get; set; }

  [JsonPropertyName("status")]
  public string OrderStatus { get; set; }

  [JsonPropertyName("replaced_by")]
  public string ReplacedByOrderId { get; set; }

  [JsonPropertyName("replaces")]
  public string ReplacesOrderId { get; set; }

  [JsonPropertyName("legs")]
  public List<JsonOrder> LegsList { get; set; } = [];
}
