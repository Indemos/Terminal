using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class BarsPageMessage
{
  [JsonPropertyName("bars")]
  public List<BarMessage> ItemsList { get; set; } = [];

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }
}
