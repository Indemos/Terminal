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
  public decimal? TradableCash { get; set; }

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
  public decimal? Multiplier { get; set; }

  [JsonPropertyName("buying_power")]
  public decimal? BuyingPower { get; set; }

  [JsonPropertyName("daytrading_buying_power")]
  public decimal? DayTradingBuyingPower { get; set; }

  [JsonPropertyName("non_maginable_buying_power")]
  public decimal? NonMarginableBuyingPower { get; set; }

  [JsonPropertyName("regt_buying_power")]
  public decimal? RegulationBuyingPower { get; set; }

  [JsonPropertyName("long_market_value")]
  public decimal? LongMarketValue { get; set; }

  [JsonPropertyName("short_market_value")]
  public decimal? ShortMarketValue { get; set; }

  [JsonPropertyName("equity")]
  public decimal? Equity { get; set; }

  [JsonPropertyName("last_equity")]
  public decimal? LastEquity { get; set; }

  [JsonPropertyName("initial_margin")]
  public decimal? InitialMargin { get; set; }

  [JsonPropertyName("maintenance_margin")]
  public decimal? MaintenanceMargin { get; set; }

  [JsonPropertyName("last_maintenance_margin")]
  public decimal? LastMaintenanceMargin { get; set; }

  [JsonPropertyName("daytrade_count")]
  public decimal? DayTradeCount { get; set; }

  [JsonPropertyName("sma")]
  public decimal? Sma { get; set; }

  [JsonPropertyName("created_at")]
  public DateTime? CreatedAtUtc { get; set; }

  [JsonPropertyName("accrued_fees")]
  public decimal? AccruedFees { get; set; }

  [JsonPropertyName("pending_transfer_in")]
  public decimal? PendingTransferIn { get; set; }

  [JsonPropertyName("pending_transfer_out")]
  public decimal? PendingTransferOut { get; set; }

  [JsonPropertyName("options_trading_level")]
  public string OptionsTradingLevel { get; set; }

  [JsonPropertyName("options_approved_level")]
  public string OptionsApprovedLevel { get; set; }

  [JsonPropertyName("options_buying_power")]
  public decimal? OptionsBuyingPower { get; set; }
}
