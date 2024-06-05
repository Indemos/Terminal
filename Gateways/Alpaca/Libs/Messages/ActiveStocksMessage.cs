using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class ActiveStocksMessage
{
  [JsonPropertyName("most_actives")]
  public List<ActiveStockMessage> MostActives { get; set; } = [];
}
