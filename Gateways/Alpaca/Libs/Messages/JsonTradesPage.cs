using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonTradesPage<TTrade>
{
  [JsonPropertyName("trades")]
  public List<TTrade> ItemsList { get; set; } = [];

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }

  [JsonIgnore]
  public List<TTrade> Items { get; set; } = [];
}
