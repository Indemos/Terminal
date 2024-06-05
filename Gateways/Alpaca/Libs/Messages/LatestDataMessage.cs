using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class LatestDataMessage<TQuote, TTrade, TSnapshot>
{
  [JsonPropertyName("quotes")]
  public Dictionary<string, TQuote> Quotes { get; set; } = [];

  [JsonPropertyName("bars")]
  public Dictionary<string, HistoricalBarMessage> Bars { get; set; } = [];

  [JsonPropertyName("trades")]
  public Dictionary<string, TTrade> Trades { get; set; } = [];

  [JsonPropertyName("snapshots")]
  public Dictionary<string, TSnapshot> Snapshots { get; set; } = [];

  [JsonPropertyName("orderbooks")]
  public Dictionary<string, HistoricalOrderBookMessage> OrderBooks { get; set; } = [];
}
