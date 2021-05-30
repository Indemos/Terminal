using Newtonsoft.Json;
using System;

namespace Gateway.Tradier.ModelSpace
{
  public class InputInstrumentModel
  {
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("symbol")]
    public string Symbol { get; set; }

    [JsonProperty("last")]
    public double? Price { get; set; }

    [JsonProperty("change")]
    public double? Change { get; set; }

    [JsonProperty("open")]
    public double? Open { get; set; }

    [JsonProperty("high")]
    public double? High { get; set; }

    [JsonProperty("low")]
    public double? Low { get; set; }

    [JsonProperty("close")]
    public double? Close { get; set; }

    [JsonProperty("bid")]
    public double? Bid { get; set; }

    [JsonProperty("ask")]
    public double? Ask { get; set; }

    [JsonProperty("bidsize")]
    public double? BidSize { get; set; }

    [JsonProperty("asksize")]
    public double? AskSize { get; set; }

    [JsonProperty("bid_date")]
    public long? BidDate { get; set; }

    [JsonProperty("ask_date")]
    public long? AskDate { get; set; }

    [JsonProperty("volume")]
    public long? Volume { get; set; }

    [JsonProperty("average_volume")]
    public long? AverageVolume { get; set; }
  }
}
