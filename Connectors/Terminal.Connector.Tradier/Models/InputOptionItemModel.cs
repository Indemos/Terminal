using Newtonsoft.Json;
using System.Collections.Generic;

namespace Gateway.Tradier.ModelSpace
{
  public class InputOptionItemModel
  {
    [JsonProperty("options")]
    public List<InputOptionModel> Options { get; set; } = new List<InputOptionModel>();
  }
}
