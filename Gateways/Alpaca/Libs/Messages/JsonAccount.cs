using System;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonAccount
{
  [JsonPropertyName("id")]
  public string AccountId { get; set; }

  [JsonPropertyName("account_number")]
  public string AccountNumber { get; set; }

  [JsonPropertyName("status")]
  public string Status { get; set; }

  [JsonPropertyName("crypto_status")]
  public string CryptoStatus { get; set; }

  [JsonPropertyName("currency")]
  public string Currency { get; set; }

  [JsonPropertyName("cash")]
  public double? TradableCash { get; set; }

  [JsonPropertyName("pattern_day_trader")]
  public bool IsDayPatternTrader { get; set; }

  [JsonPropertyName("trading_blocked")]
  public bool IsTradingBlocked { get; set; }

  [JsonPropertyName("transfers_blocked")]
  public bool IsTransfersBlocked { get; set; }

  [JsonPropertyName("account_blocked")]
  public bool IsAccountBlocked { get; set; }

  [JsonPropertyName("trade_suspended_by_user")]
  public bool TradeSuspendedByUser { get; set; }

  [JsonPropertyName("shorting_enabled")]
  public bool ShortingEnabled { get; set; }

  [JsonPropertyName("multiplier")]
  public double? Multiplier { get; set; }

  [JsonPropertyName("buying_power")]
  public double? BuyingPower { get; set; }

  [JsonPropertyName("daytrading_buying_power")]
  public double? DayTradingBuyingPower { get; set; }

  [JsonPropertyName("non_maginable_buying_power")]
  public double? NonMarginableBuyingPower { get; set; }

  [JsonPropertyName("regt_buying_power")]
  public double? RegulationBuyingPower { get; set; }

  [JsonPropertyName("long_market_value")]
  public double? LongMarketValue { get; set; }

  [JsonPropertyName("short_market_value")]
  public double? ShortMarketValue { get; set; }

  [JsonPropertyName("equity")]
  public double? Equity { get; set; }

  [JsonPropertyName("last_equity")]
  public double? LastEquity { get; set; }

  [JsonPropertyName("initial_margin")]
  public double? InitialMargin { get; set; }

  [JsonPropertyName("maintenance_margin")]
  public double? MaintenanceMargin { get; set; }

  [JsonPropertyName("last_maintenance_margin")]
  public double? LastMaintenanceMargin { get; set; }

  [JsonPropertyName("daytrade_count")]
  public double? DayTradeCount { get; set; }

  [JsonPropertyName("sma")]
  public double? Sma { get; set; }

  [JsonPropertyName("created_at")]
  public DateTime? CreatedAtUtc { get; set; }

  [JsonPropertyName("accrued_fees")]
  public double? AccruedFees { get; set; }

  [JsonPropertyName("pending_transfer_in")]
  public double? PendingTransferIn { get; set; }

  [JsonPropertyName("pending_transfer_out")]
  public double? PendingTransferOut { get; set; }

  [JsonPropertyName("options_trading_level")]
  public string OptionsTradingLevel { get; set; }

  [JsonPropertyName("options_approved_level")]
  public string OptionsApprovedLevel { get; set; }

  [JsonPropertyName("options_buying_power")]
  public double? OptionsBuyingPower { get; set; }
}
