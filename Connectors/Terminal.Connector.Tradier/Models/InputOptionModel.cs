using Newtonsoft.Json;
using System;

namespace Gateway.Tradier.ModelSpace
{
  public class InputOptionModel
  {
    [JsonProperty("symbol")]
    public string Name { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("underlying")]
    public string Symbol { get; set; }

    [JsonProperty("strike")]
    public double? Strike { get; set; }

    [JsonProperty("last")]
    public double? Last { get; set; }

    [JsonProperty("change")]
    public double? Change { get; set; }

    [JsonProperty("volume")]
    public double? Volune { get; set; }

    [JsonProperty("bid")]
    public double? Bid { get; set; }

    [JsonProperty("ask")]
    public double? Ask { get; set; }

    [JsonProperty("bidsize")]
    public double? BidSize { get; set; }

    [JsonProperty("asksize")]
    public double? AskSize { get; set; }

    [JsonProperty("open_interest")]
    public double? OpenInterest { get; set; }

    [JsonProperty("contract_size")]
    public double? Leverage { get; set; }

    [JsonProperty("bid_date")]
    public long? BidDate { get; set; }

    [JsonProperty("ask_date")]
    public long? AskDate { get; set; }

    [JsonProperty("expiration_date")]
    public DateTime? ExpirationDate { get; set; }
  }
}
