using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class PositionActionStatusMessage
{
  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("status")]
  public double? StatusCode { get; set; }
}
