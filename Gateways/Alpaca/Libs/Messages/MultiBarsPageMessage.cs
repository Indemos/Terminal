using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class MultiBarsPageMessage
{
  [JsonPropertyName("bars")]
  public Dictionary<string, List<HistoricalBarMessage>> ItemsDictionary { get; set; } = [];

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }

  [JsonIgnore]
  public Dictionary<string, List<RealTimeBarMessage>> Items { get; set; } = [];
}
