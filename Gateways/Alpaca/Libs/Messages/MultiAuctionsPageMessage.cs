using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class MultiAuctionsPageMessage
{
  [JsonPropertyName("auctions")]
  public Dictionary<string, List<AuctionMessage>> ItemsDictionary { get; set; } = [];

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }

  [JsonIgnore]
  public Dictionary<string, List<AuctionMessage>> Items { get; set; } = [];
}
