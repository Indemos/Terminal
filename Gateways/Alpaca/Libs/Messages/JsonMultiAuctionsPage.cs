using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonMultiAuctionsPage
{
  [JsonPropertyName("auctions")]
  public Dictionary<string, List<JsonAuction>> ItemsDictionary { get; set; } = [];

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }

  [JsonIgnore]
  public Dictionary<string, List<JsonAuction>> Items { get; set; } = [];
}
