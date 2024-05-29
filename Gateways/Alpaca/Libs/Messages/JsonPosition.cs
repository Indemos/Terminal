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
  public decimal? AverageEntryPrice { get; set; }

  [JsonPropertyName("qty")]
  public decimal? Quantity { get; set; }

  [JsonPropertyName("qty_available")]
  public decimal? AvailableQuantity { get; set; }

  [JsonPropertyName("side")]
  public string Side { get; set; }

  [JsonPropertyName("market_value")]
  public decimal? MarketValue { get; set; }

  [JsonPropertyName("cost_basis")]
  public decimal? CostBasis { get; set; }

  [JsonPropertyName("unrealized_pl")]
  public decimal? UnrealizedProfitLoss { get; set; }

  [JsonPropertyName("unrealized_plpc")]
  public decimal? UnrealizedProfitLossPercent { get; set; }

  [JsonPropertyName("unrealized_intraday_pl")]
  public decimal? IntradayUnrealizedProfitLoss { get; set; }

  [JsonPropertyName("unrealized_intraday_plpc")]
  public decimal? IntradayUnrealizedProfitLossPercent { get; set; }

  [JsonPropertyName("current_price")]
  public decimal? AssetCurrentPrice { get; set; }

  [JsonPropertyName("lastday_price")]
  public decimal? AssetLastPrice { get; set; }

  [JsonPropertyName("change_today")]
  public decimal? AssetChangePercent { get; set; }
}
