using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonQuotesPage<TQuote>
{
  [JsonPropertyName("quotes")]
  public List<TQuote> ItemsList { get; set; } = [];

  [JsonPropertyName("symbol")]
  public string Symbol { get; set; }

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }

  [JsonIgnore]
  public List<TQuote> Items { get; set; } = [];
}
