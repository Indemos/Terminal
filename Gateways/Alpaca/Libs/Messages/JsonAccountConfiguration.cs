using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonAccountConfiguration
{
  [JsonPropertyName("dtbp_check")]
  public string DayTradeMarginCallProtection { get; set; }

  [JsonPropertyName("trade_confirm_email")]
  public string TradeConfirmEmail { get; set; }

  [JsonPropertyName("suspend_trade")]
  public bool IsSuspendTrade { get; set; }

  [JsonPropertyName("no_shorting")]
  public bool IsNoShorting { get; set; }

  [JsonPropertyName("ptp_no_exception_entry")]
  public bool IsPtpNoExceptionEntry { get; set; }

  [JsonPropertyName("max_options_trading_level")]
  public string MaxOptionsTradingLevel { get; set; }
}
