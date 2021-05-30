using Newtonsoft.Json;

namespace Gateway.Tradier.ModelSpace
{
  public class InputOptionVarianceModel
  {
    [JsonProperty("delta")]
    public double? Delta { get; set; }

    [JsonProperty("gamma")]
    public double? Gamma { get; set; }

    [JsonProperty("theta")]
    public double? Theta { get; set; }

    [JsonProperty("vega")]
    public double? Vega { get; set; }

    [JsonProperty("rho")]
    public double? Rho { get; set; }

    [JsonProperty("phi")]
    public double? Phi { get; set; }

    [JsonProperty("bid_iv")]
    public double? BidIv { get; set; }

    [JsonProperty("ask_iv")]
    public double? AskIv { get; set; }

    [JsonProperty("mid_iv")]
    public double? MidIv { get; set; }

    [JsonProperty("smv_vol")]
    public double? Iv { get; set; }
  }
}
