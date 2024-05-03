using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonError
{
  [JsonPropertyName("code")]
  public int? Code { get; set; }

  [JsonPropertyName("message")]
  public string Message { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("open_orders")]
  public int? OpenOrdersCount { get; set; }

  [JsonPropertyName("day_trading_buying_power")]
  public double? DayTradingBuyingPower { get; set; }

  [JsonPropertyName("max_dtbp_used")]
  public double? MaxDayTradingBuyingPowerUsed { get; set; }

  [JsonPropertyName("max_dtbp_used_so_far")]
  public double? MaxDayTradingBuyingPowerUsedSoFar { get; set; }
}
