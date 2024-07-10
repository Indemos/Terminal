using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class DataMessage<T>
{
  [JsonPropertyName("quotes")]
  public Dictionary<string, QuoteMessage> Quotes { get; set; } = [];

  [JsonPropertyName("bars")]
  public Dictionary<string, BarMessage> Bars { get; set; } = [];

  [JsonPropertyName("trades")]
  public Dictionary<string, TradeMessage> Trades { get; set; } = [];

  [JsonPropertyName("orderbooks")]
  public Dictionary<string, OrderBookMessage> OrderBooks { get; set; } = [];

  [JsonPropertyName("snapshots")]
  public Dictionary<string, T> Snapshots { get; set; } = [];
}
