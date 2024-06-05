using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class OptionContractsPageMessage
{
  [JsonPropertyName("option_contracts")]
  public List<OptionContractMessage> Contracts { get; set; } = [];
}
