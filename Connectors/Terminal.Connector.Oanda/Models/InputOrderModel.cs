using Newtonsoft.Json;
using System;

namespace Gateway.Oanda.ModelSpace
{
  public class InputOrderModel
  {
    [JsonProperty("id")]
    public int? Id { get; set; }

    [JsonProperty("fillingTransactionID")]
    public int? FillingTransactionId { get; set; }

    [JsonProperty("triggeringTransactionID")]
    public int? TriggeringTransactionId { get; set; }

    [JsonProperty("cancellingTransactionID")]
    public int? CancellingTransactionId { get; set; }

    [JsonProperty("units")]
    public double? Size { get; set; }

    [JsonProperty("price")]
    public double? Price { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("state")]
    public string Status { get; set; }

    [JsonProperty("instrument")]
    public string Instrument { get; set; }

    [JsonProperty("timeInForce")]
    public string TimeSpan { get; set; }

    [JsonProperty("filledTime")]
    public DateTime? FillTime { get; set; }

    [JsonProperty("triggeredTime")]
    public DateTime? TriggerTime { get; set; }

    [JsonProperty("createTime")]
    public DateTime? CreationTime { get; set; }

    [JsonProperty("cancelledTime")]
    public DateTime? CancellationTime { get; set; }
  }
}
