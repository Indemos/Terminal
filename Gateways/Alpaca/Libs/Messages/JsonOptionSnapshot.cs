using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonOptionSnapshot
{
  [JsonPropertyName("latestQuote")]
  public JsonOptionQuote JsonQuote { get; set; }

  [JsonPropertyName("latestTrade")]
  public JsonOptionTrade JsonTrade { get; set; }
}
