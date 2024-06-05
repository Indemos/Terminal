using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class BarsPageMessage
{
  [JsonPropertyName("bars")]
  public List<HistoricalBarMessage> ItemsList { get; set; } = [];

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }

  [JsonIgnore]
  public List<RealTimeBarMessage> Items { get; set; } = [];
}
