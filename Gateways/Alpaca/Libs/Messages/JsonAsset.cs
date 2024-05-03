using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonAsset
{
  [JsonPropertyName("id")]
  public string AssetId { get; set; }

  [JsonPropertyName("class")]
  public string Class { get; set; }

  [JsonPropertyName("exchange")]
  public string Exchange { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("status")]
  public string Status { get; set; }

  [JsonPropertyName("tradable")]
  public bool IsTradable { get; set; }

  [JsonPropertyName("marginable")]
  public bool Marginable { get; set; }

  [JsonPropertyName("shortable")]
  public bool Shortable { get; set; }

  [JsonPropertyName("easy_to_borrow")]
  public bool EasyToBorrow { get; set; }

  [JsonPropertyName("fractionable")]
  public bool Fractionable { get; set; }

  [JsonPropertyName("min_order_size")]
  public double? MinOrderSize { get; set; }

  [JsonPropertyName("min_trade_increment")]
  public double? MinTradeIncrement { get; set; }

  [JsonPropertyName("price_increment")]
  public double? PriceIncrement { get; set; }

  [JsonPropertyName("maintenance_margin_requirement")]
  public double? MaintenanceMarginRequirement { get; set; }

  [JsonPropertyName("attributes")]
  public List<string> AttributesList { get; set; } = [];
}
