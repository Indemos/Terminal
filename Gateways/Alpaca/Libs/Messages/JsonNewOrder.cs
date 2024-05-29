using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonNewOrder
{
  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("qty")]
  public decimal? Quantity { get; set; }

  [JsonPropertyName("notional")]
  public decimal? Notional { get; set; }

  [JsonPropertyName("side")]
  public string OrderSide { get; set; }

  [JsonPropertyName("type")]
  public string OrderType { get; set; }

  [JsonPropertyName("time_in_force")]
  public string TimeInForce { get; set; }

  [JsonPropertyName("limit_price")]
  public decimal? LimitPrice { get; set; }

  [JsonPropertyName("stop_price")]
  public decimal? StopPrice { get; set; }

  [JsonPropertyName("trail_price")]
  public decimal? TrailOffsetInDollars { get; set; }

  [JsonPropertyName("trail_percent")]
  public decimal? TrailOffsetInPercent { get; set; }

  [JsonPropertyName("client_order_id")]
  public string ClientOrderId { get; set; }

  [JsonPropertyName("extended_hours")]
  public bool? ExtendedHours { get; set; }

  [JsonPropertyName("order_class")]
  public string OrderClass { get; set; }

  [JsonPropertyName("take_profit")]
  public JsonNewOrderAdvancedAttributes TakeProfit { get; set; }

  [JsonPropertyName("stop_loss")]
  public JsonNewOrderAdvancedAttributes StopLoss { get; set; }
}
