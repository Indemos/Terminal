using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class ActiveStocksMessage
{
  [JsonPropertyName("most_actives")]
  public List<ActiveStockMessage> MostActives { get; set; } = [];
}
