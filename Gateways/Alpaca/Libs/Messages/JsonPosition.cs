using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonPosition
{
  [JsonPropertyName("asset_id")]
  public string AssetId { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("exchange")]
  public string Exchange { get; set; }

  [JsonPropertyName("asset_class")]
  public string AssetClass { get; set; }

  [JsonPropertyName("avg_entry_price")]
  public double? AverageEntryPrice { get; set; }

  [JsonPropertyName("qty")]
  public double? Quantity { get; set; }

  [JsonPropertyName("qty_available")]
  public double? AvailableQuantity { get; set; }

  [JsonPropertyName("side")]
  public string Side { get; set; }

  [JsonPropertyName("market_value")]
  public double? MarketValue { get; set; }

  [JsonPropertyName("cost_basis")]
  public double? CostBasis { get; set; }

  [JsonPropertyName("unrealized_pl")]
  public double? UnrealizedProfitLoss { get; set; }

  [JsonPropertyName("unrealized_plpc")]
  public double? UnrealizedProfitLossPercent { get; set; }

  [JsonPropertyName("unrealized_intraday_pl")]
  public double? IntradayUnrealizedProfitLoss { get; set; }

  [JsonPropertyName("unrealized_intraday_plpc")]
  public double? IntradayUnrealizedProfitLossPercent { get; set; }

  [JsonPropertyName("current_price")]
  public double? AssetCurrentPrice { get; set; }

  [JsonPropertyName("lastday_price")]
  public double? AssetLastPrice { get; set; }

  [JsonPropertyName("change_today")]
  public double? AssetChangePercent { get; set; }
}
