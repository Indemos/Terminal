using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class MultiQuotesPageMessage<TQuote>
{
  [JsonPropertyName("quotes")]
  public Dictionary<string, List<TQuote>> ItemsDictionary { get; set; } = [];

  [JsonPropertyName("next_page_token")]
  public string NextPageToken { get; set; }

  [JsonIgnore]
  public Dictionary<string, List<TQuote>> Items { get; set; } = [];
}
