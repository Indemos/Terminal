using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonOptionContractsPage
{
  [JsonPropertyName("option_contracts")]
  public List<JsonOptionContract> Contracts { get; set; } = [];
}
