using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class OrderActionStatusMessage
{
  [JsonPropertyName("id")]
  public string OrderId { get; set; }

  [JsonPropertyName("status")]
  public double? StatusCode { get; set; }
}
