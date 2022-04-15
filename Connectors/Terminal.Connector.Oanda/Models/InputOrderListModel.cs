using Newtonsoft.Json;
using System.Collections.Generic;

namespace Gateway.Oanda.ModelSpace
{
  public class InputOrderListModel
  {
    [JsonProperty("orders")]
    public List<InputOrderModel> Orders { get; set; } = new List<InputOrderModel>();
  }
}
