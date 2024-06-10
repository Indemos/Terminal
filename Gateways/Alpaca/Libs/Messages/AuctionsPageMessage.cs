using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class AuctionsPageMessage
{
  [JsonPropertyName("auctions")]
  public List<AuctionMessage> ItemsList { get; set; } = [];

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }

  [JsonIgnore]
  public List<AuctionMessage> Items { get; set; } = [];
}
