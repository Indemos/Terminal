using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonSubscriptionUpdate
{
  [JsonPropertyName("action")]
  public string Action { get; set; }

  [JsonPropertyName("trades")]
  public List<string> Trades { get; set; } = [];

  [JsonPropertyName("quotes")]
  public List<string> Quotes { get; set; } = [];

  [JsonPropertyName("bars")]
  public List<string> MinuteBars { get; set; } = [];

  [JsonPropertyName("dailyBars")]
  public List<string> DailyBars { get; set; } = [];

  [JsonPropertyName("statuses")]
  public List<string> Statuses { get; set; } = [];

  [JsonPropertyName("lulds")]
  public List<string> Lulds { get; set; } = [];

  [JsonPropertyName("news")]
  public List<string> News { get; set; } = [];

  [JsonPropertyName("updatedBars")]
  public List<string> UpdatedBars { get; set; } = [];

  [JsonPropertyName("orderbooks")]
  public List<string> OrderBooks { get; set; } = [];
}
