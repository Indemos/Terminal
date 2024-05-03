using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonLatestQuote<TQuote>
{
  [JsonPropertyName("quote")]
  public TQuote Nested { get; set; }

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }
}
