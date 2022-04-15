using Newtonsoft.Json;

namespace Gateway.Oanda.ModelSpace
{
  public class InputAccountModel
  {
    [JsonProperty("openTradeCount")]
    public int? OpenTradesCount { get; set; }

    [JsonProperty("pendingOrderCount")]
    public int? OpenOrdersCount { get; set; }

    [JsonProperty("openPositionCount")]
    public int? OpenPositionsCount { get; set; }

    [JsonProperty("pl")]
    public double? PnL { get; set; }

    [JsonProperty("unrealizedPL")]
    public double? ActivePnL { get; set; }

    [JsonProperty("balance")]
    public double? Balance { get; set; }

    [JsonProperty("commission")]
    public double? Commission { get; set; }

    [JsonProperty("marginRate")]
    public double? MarginRate { get; set; }

    [JsonProperty("marginUsed")]
    public double? MarginInUse { get; set; }

    [JsonProperty("marginAvailable")]
    public double? MarginAvailable { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("alias")]
    public string Alias { get; set; }

    [JsonProperty("currency")]
    public string Currency { get; set; }
  }
}
