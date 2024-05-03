using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonPositionActionStatus
{
  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("status")]
  public double? StatusCode { get; set; }
}
