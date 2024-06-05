using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class MarketMoversMessage
{
  [JsonPropertyName("losers")]
  public List<MarketMoverMessage> LosersList { get; set; } = [];

  [JsonPropertyName("gainers")]
  public List<MarketMoverMessage> GainersList { get; set; } = [];
}
