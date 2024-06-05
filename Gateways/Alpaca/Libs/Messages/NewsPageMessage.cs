using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class NewsPageMessage
{
  [JsonPropertyName("news")]
  public List<NewsArticleMessage> ItemsList { get; set; } = [];

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }
}
