using Newtonsoft.Json;
using System.Collections.Generic;

namespace Gateway.Tradier.ModelSpace
{
  public class InputOptionChainDataModel
  {
    [JsonProperty("options")]
    public InputOptionChainItemsModel Chains { get; set; } = new InputOptionChainItemsModel();
  }

  public class InputOptionChainItemsModel
  {
    [JsonProperty("option")]
    public List<InputOptionModel> Chain { get; set; } = new List<InputOptionModel>();
  }
}
