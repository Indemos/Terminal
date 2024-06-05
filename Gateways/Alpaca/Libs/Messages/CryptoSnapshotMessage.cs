using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class CryptoSnapshotMessage
{
  [JsonPropertyName("latestQuote")]
  public HistoricalCryptoQuoteMessage JsonQuote { get; set; }

  [JsonPropertyName("latestTrade")]
  public HistoricalTradeMessage JsonTrade { get; set; }

  [JsonPropertyName("minuteBar")]
  public HistoricalBarMessage JsonMinuteBar { get; set; }

  [JsonPropertyName("dailyBar")]
  public HistoricalBarMessage JsonCurrentDailyBar { get; set; }

  [JsonPropertyName("prevDailyBar")]
  public HistoricalBarMessage JsonPreviousDailyBar { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
