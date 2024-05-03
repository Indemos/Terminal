using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonNewsPage
{
  [JsonPropertyName("news")]
  public List<JsonNewsArticle> ItemsList { get; set; } = [];

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }
}
