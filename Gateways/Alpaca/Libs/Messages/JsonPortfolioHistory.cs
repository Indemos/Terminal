using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonPortfolioHistory
{
  public class Item
  {
    public double? Equity { get; set; }

    public double? ProfitLoss { get; set; }

    public double? ProfitLossPercentage { get; set; }

    public DateTime? TimestampUtc { get; set; }
  }

  [JsonPropertyName("equity")]
  public List<double?> EquityList { get; set; } = [];

  [JsonPropertyName("profit_loss")]
  public List<double?> ProfitLossList { get; set; } = [];

  [JsonPropertyName("profit_loss_pct")]
  public List<double?> ProfitLossPercentageList { get; set; } = [];

  [JsonPropertyName("timestamp")]
  public List<DateTime?> TimestampsList { get; set; } = [];

  [JsonPropertyName("timeframe")]
  public string TimeFrame { get; set; }

  [JsonPropertyName("base_value")]
  public double? BaseValue { get; set; }
}
