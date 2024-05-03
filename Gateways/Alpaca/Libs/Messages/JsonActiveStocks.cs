using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonActiveStocks
{
  [JsonPropertyName("most_actives")]
  public List<JsonActiveStock> MostActives { get; set; } = [];
}
