using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class SnapshotMessage
{
  [JsonPropertyName("latestQuote")]
  public QuoteMessage JsonQuote { get; set; }

  [JsonPropertyName("latestTrade")]
  public TradeMessage JsonTrade { get; set; }

  [JsonPropertyName("minuteBar")]
  public BarMessage JsonMinuteBar { get; set; }

  [JsonPropertyName("dailyBar")]
  public BarMessage JsonCurrentDailyBar { get; set; }

  [JsonPropertyName("prevDailyBar")]
  public BarMessage JsonPreviousDailyBar { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
