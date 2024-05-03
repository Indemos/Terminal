using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonOrderBookEntry
{
  [JsonPropertyName("p")]
  public double? Price { get; set; }

  [JsonPropertyName("s")]
  public double? Size { get; set; }
}
