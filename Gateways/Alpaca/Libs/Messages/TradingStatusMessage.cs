using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class TradingStatusMessage
{
  [JsonPropertyName("sc")]
  public string StatusCode { get; set; }

  [JsonPropertyName("sm")]
  public string StatusMessage { get; set; }

  [JsonPropertyName("rc")]
  public string ReasonCode { get; set; }

  [JsonPropertyName("rm")]
  public string ReasonMessage { get; set; }

  [JsonPropertyName("z")]
  public string Tape { get; set; }
}
