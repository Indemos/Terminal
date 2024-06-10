using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class NewsPageMessage
{
  [JsonPropertyName("news")]
  public List<NewsArticleMessage> ItemsList { get; set; } = [];

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }
}
