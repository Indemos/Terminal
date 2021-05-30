using Newtonsoft.Json;
using System.Collections.Generic;

namespace Gateway.Tradier.ModelSpace
{
  public class InputInstrumentDataModel
  {
    [JsonProperty("quotes")]
    public InputInstrumentItemsModel Quotes { get; set; } = new InputInstrumentItemsModel();
  }

  public class InputInstrumentItemsModel
  {
    [JsonProperty("quote")]
    public List<InputInstrumentModel> Quote { get; set; } = new List<InputInstrumentModel>();
  }
}
