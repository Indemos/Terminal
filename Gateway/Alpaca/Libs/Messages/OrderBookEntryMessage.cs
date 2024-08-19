using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class OrderBookEntryMessage
{
  [JsonPropertyName("p")]
  public double? Price { get; set; }

  [JsonPropertyName("s")]
  public double? Size { get; set; }
}
