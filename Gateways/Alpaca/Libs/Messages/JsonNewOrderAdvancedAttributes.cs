using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonNewOrderAdvancedAttributes
{
  [JsonPropertyName("limit_price")]
  public decimal? LimitPrice { get; set; }

  [JsonPropertyName("stop_price")]
  public decimal? StopPrice { get; set; }
}
