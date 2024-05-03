using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonLimitUpLimitDown
{
  [JsonPropertyName("u")]
  public double? LimitUpPrice { get; set; }

  [JsonPropertyName("d")]
  public double? LimitDownPrice { get; set; }

  [JsonPropertyName("i")]
  public string Indicator { get; set; }

  [JsonPropertyName("z")]
  public string Tape { get; set; }
}
