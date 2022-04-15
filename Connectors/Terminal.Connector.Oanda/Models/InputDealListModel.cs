using Newtonsoft.Json;
using System.Collections.Generic;

namespace Gateway.Oanda.ModelSpace
{
  public class InputDealListModel
  {
    [JsonProperty("trades")]
    public List<InputDealModel> Deals { get; set; } = new List<InputDealModel>();
  }
}
