using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class LatestBestBidOfferMessage
{
  [JsonPropertyName("xbbo")]
  public HistoricalQuoteMessage Nested { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
