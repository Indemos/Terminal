using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Gateway.Tradier.ModelSpace
{
  public class InputOptionDateDataModel
  {
    [JsonProperty("expirations")]
    public InputOptionDateItemsModel Dates { get; set; } = new InputOptionDateItemsModel();
  }

  public class InputOptionDateItemsModel
  {
    [JsonProperty("date")]
    public List<DateTime> Date { get; set; } = new List<DateTime>();
  }
}
