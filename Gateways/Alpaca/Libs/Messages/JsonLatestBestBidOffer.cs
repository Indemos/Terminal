using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonLatestBestBidOffer
{
  [JsonPropertyName("xbbo")]
  public JsonHistoricalQuote Nested { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
