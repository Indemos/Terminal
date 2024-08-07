using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class OrderCreationMessage
{
  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("qty")]
  public double? Quantity { get; set; }

  [JsonPropertyName("notional")]
  public double? Notional { get; set; }

  [JsonPropertyName("side")]
  public string OrderSide { get; set; }

  [JsonPropertyName("type")]
  public string OrderType { get; set; }

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

  [JsonPropertyName("client_order_id")]
  public string ClientOrderId { get; set; }

  [JsonPropertyName("extended_hours")]
  public bool? ExtendedHours { get; set; }

  [JsonPropertyName("order_class")]
  public string OrderClass { get; set; }

  [JsonPropertyName("take_profit")]
  public OrderBracketMessage TakeProfit { get; set; }

  [JsonPropertyName("stop_loss")]
  public OrderBracketMessage StopLoss { get; set; }
}
