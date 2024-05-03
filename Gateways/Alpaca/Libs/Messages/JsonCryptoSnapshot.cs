using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonCryptoSnapshot
{
  [JsonPropertyName("latestQuote")]
  public JsonHistoricalCryptoQuote JsonQuote { get; set; }

  [JsonPropertyName("latestTrade")]
  public JsonHistoricalTrade JsonTrade { get; set; }

  [JsonPropertyName("minuteBar")]
  public JsonHistoricalBar JsonMinuteBar { get; set; }

  [JsonPropertyName("dailyBar")]
  public JsonHistoricalBar JsonCurrentDailyBar { get; set; }

  [JsonPropertyName("prevDailyBar")]
  public JsonHistoricalBar JsonPreviousDailyBar { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
