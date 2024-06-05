using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class PositionActionStatusMessage
{
  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("status")]
  public double? StatusCode { get; set; }
}
