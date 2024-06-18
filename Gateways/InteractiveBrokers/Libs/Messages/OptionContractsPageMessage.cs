using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class OptionContractsPageMessage
{
  [JsonPropertyName("option_contracts")]
  public List<OptionContractMessage> Contracts { get; set; } = [];
}
