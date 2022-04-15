using Newtonsoft.Json;
using System;

namespace Gateway.Oanda.ModelSpace
{
  public class InputDealModel : InputOrderModel
  {
    [JsonProperty("initialUnits")]
    public double? InitialSize { get; set; }

    [JsonProperty("currentUnits")]
    public double? CurrentSize { get; set; }

    [JsonProperty("realizedPL")]
    public double? PnL { get; set; }

    [JsonProperty("unrealizedPL")]
    public double? ActivePnL { get; set; }

    [JsonProperty("marginUsed")]
    public double? Margin { get; set; }

    [JsonProperty("initialMarginRequired")]
    public double? InitialMargin { get; set; }

    [JsonProperty("openTime")]
    public DateTime? OpenTime { get; set; }
  }
}
