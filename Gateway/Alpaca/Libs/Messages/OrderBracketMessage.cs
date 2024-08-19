using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class OrderBracketMessage
{
  [JsonPropertyName("limit_price")]
  public double? LimitPrice { get; set; }

  [JsonPropertyName("stop_price")]
  public double? StopPrice { get; set; }
}
