using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonMarketMovers
{
  [JsonPropertyName("losers")]
  public List<JsonMarketMover> LosersList { get; set; } = [];

  [JsonPropertyName("gainers")]
  public List<JsonMarketMover> GainersList { get; set; } = [];
}
