using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonMultiTradesPage<TTrade>
{
  [JsonPropertyName("trades")]
  public Dictionary<string, List<TTrade>> ItemsDictionary { get; set; } = [];

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }

  [JsonIgnore]
  public Dictionary<string, List<TTrade>> Items { get; set; } = [];
}
