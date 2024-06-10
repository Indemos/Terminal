using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class LatestQuoteMessage<TQuote>
{
  [JsonPropertyName("quote")]
  public TQuote Nested { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
