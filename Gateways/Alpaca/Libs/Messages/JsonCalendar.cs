using System;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonCalendar
{
  [JsonPropertyName("date")]
  public DateOnly? TradingDate { get; set; }

  [JsonPropertyName("open")]
  public TimeOnly? TradingOpen { get; set; }

  [JsonPropertyName("close")]
  public TimeOnly? TradingClose { get; set; }

  [JsonPropertyName("session_open")]
  public TimeOnly? SessionOpen { get; set; }

  [JsonPropertyName("session_close")]
  public TimeOnly? SessionClose { get; set; }

  [JsonIgnore]
  public string Trading { get; private set; }

  [JsonIgnore]
  public string Session { get; private set; }
}
