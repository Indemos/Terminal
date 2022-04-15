using Newtonsoft.Json;

namespace Gateway.Oanda.ModelSpace
{
  public class InputAccountItemModel
  {
    [JsonProperty("account")]
    public InputAccountModel Account { get; set; } = new InputAccountModel();
  }
}
