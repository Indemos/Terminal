using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class OptionSnapshotMessage
{
  [JsonPropertyName("latestQuote")]
  public OptionQuoteMessage Quote { get; set; }

  [JsonPropertyName("latestTrade")]
  public OptionTradeMessage Trade { get; set; }
}
