using Newtonsoft.Json;
using System.Collections.Generic;

namespace Gateway.Tradier.ModelSpace
{
  public class InputOptionStrikeDataModel
  {
    [JsonProperty("strikes")]
    public InputOptionStrikeItemsModel Strikes { get; set; } = new InputOptionStrikeItemsModel();
  }

  public class InputOptionStrikeItemsModel
  {
    [JsonProperty("strike")]
    public List<double> Strike { get; set; } = new List<double>();
  }
}
