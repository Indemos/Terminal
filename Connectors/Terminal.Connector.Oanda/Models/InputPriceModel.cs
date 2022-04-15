using Newtonsoft.Json;

namespace Gateway.Oanda.ModelSpace
{
  public class InputPriceModel
  {
    [JsonProperty("liquidity")]
    public double? Size { get; set; }

    [JsonProperty("price")]
    public double? Price { get; set; }
  }
}
