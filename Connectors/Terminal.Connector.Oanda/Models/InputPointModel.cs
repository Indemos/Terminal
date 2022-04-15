using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Gateway.Oanda.ModelSpace
{
  public class InputPointModel
  {
    [JsonProperty("tradeable")]
    public bool? Tradeable { get; set; }

    [JsonProperty("closeoutBid")]
    public double? Bid { get; set; }

    [JsonProperty("closeoutAsk")]
    public double? Ask { get; set; }

    [JsonProperty("instrument")]
    public string Instrument { get; set; } = string.Empty;

    [JsonProperty("time")]
    public DateTime? Time { get; set; }

    [JsonProperty("bids")]
    public List<InputPriceModel> Bids { get; set; } = new List<InputPriceModel>();

    [JsonProperty("asks")]
    public List<InputPriceModel> Asks { get; set; } = new List<InputPriceModel>();
  }
}
