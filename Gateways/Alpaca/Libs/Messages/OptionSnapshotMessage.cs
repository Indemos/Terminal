using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class OptionSnapshotMessage
{
  [JsonPropertyName("latestQuote")]
  public OptionQuoteMessage JsonQuote { get; set; }

  [JsonPropertyName("latestTrade")]
  public OptionTradeMessage JsonTrade { get; set; }
}
