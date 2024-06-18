using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class MarketMoversMessage
{
  [JsonPropertyName("losers")]
  public List<MarketMoverMessage> LosersList { get; set; } = [];

  [JsonPropertyName("gainers")]
  public List<MarketMoverMessage> GainersList { get; set; } = [];
}
