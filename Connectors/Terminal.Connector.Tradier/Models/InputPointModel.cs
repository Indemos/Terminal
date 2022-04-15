using Newtonsoft.Json;
using System;

namespace Gateway.Tradier.ModelSpace
{
  public class InputPointModel
  {
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("symbol")]
    public string Symbol { get; set; }

    [JsonProperty("bid")]
    public double? Bid { get; set; }

    [JsonProperty("ask")]
    public double? Ask { get; set; }

    [JsonProperty("bidsz")]
    public double? BidSize { get; set; }

    [JsonProperty("asksz")]
    public double? AskSize { get; set; }

    [JsonProperty("biddate")]
    public long? BidDate { get; set; }

    [JsonProperty("askdate")]
    public long? AskDate { get; set; }
  }
}
