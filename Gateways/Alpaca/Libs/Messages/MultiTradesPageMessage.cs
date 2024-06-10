using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class MultiTradesPageMessage<TTrade>
{
  [JsonPropertyName("trades")]
  public Dictionary<string, List<TTrade>> ItemsDictionary { get; set; } = [];

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }

  [JsonIgnore]
  public Dictionary<string, List<TTrade>> Items { get; set; } = [];
}
