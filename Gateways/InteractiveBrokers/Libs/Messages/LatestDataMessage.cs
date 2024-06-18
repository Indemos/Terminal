using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class LatestDataMessage<TQuote, TBar, TTrade, TSnapshot, TOrderBook>
{
  [JsonPropertyName("quotes")]
  public Dictionary<string, TQuote> Quotes { get; set; } = [];

  [JsonPropertyName("bars")]
  public Dictionary<string, TBar> Bars { get; set; } = [];

  [JsonPropertyName("trades")]
  public Dictionary<string, TTrade> Trades { get; set; } = [];

  [JsonPropertyName("snapshots")]
  public Dictionary<string, TSnapshot> Snapshots { get; set; } = [];

  [JsonPropertyName("orderbooks")]
  public Dictionary<string, TOrderBook> OrderBooks { get; set; } = [];
}
