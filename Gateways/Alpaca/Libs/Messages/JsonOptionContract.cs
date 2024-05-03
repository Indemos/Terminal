using System;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonOptionContract
{
  [JsonPropertyName("id")]
  public string ContractId { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("status")]
  public string Status { get; set; }

  [JsonPropertyName("tradable")]
  public bool IsTradable { get; set; }

  [JsonPropertyName("size")]
  public double? Size { get; set; }

  [JsonPropertyName("type")]
  public string OptionType { get; }

  [JsonPropertyName("strike_price")]
  public double? StrikePrice { get; set; }

  [JsonPropertyName("expiration_date")]
  public DateOnly? ExpirationDate { get; set; }

  [JsonPropertyName("style")]
  public string OptionStyle { get; }

  [JsonPropertyName("root_symbol")]
  public string RootSymbol { get; set; }

  [JsonPropertyName("underlying_symbol")]
  public string UnderlyingSymbol { get; set; }

  [JsonPropertyName("underlying_asset_id")]
  public string UnderlyingAssetId { get; set; }

  [JsonPropertyName("open_interest")]
  public double? OpenInterest { get; set; }

  [JsonPropertyName("open_interest_date")]
  public DateOnly? OpenInterestDate { get; set; }

  [JsonPropertyName("close_price")]
  public double? ClosePrice { get; set; }

  [JsonPropertyName("close_price_date")]
  public DateOnly? ClosePriceDate { get; set; }
}
