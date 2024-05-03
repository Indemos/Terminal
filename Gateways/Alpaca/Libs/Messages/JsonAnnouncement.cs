using System;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonAnnouncement
{
  [JsonPropertyName("id")]
  public string Id { get; set; }

  [JsonPropertyName("corporate_action_id")]
  public string CorporateActionId { get; set; }

  [JsonPropertyName("ca_type")]
  public string Type { get; set; }

  [JsonPropertyName("ca_sub_type")]
  public string SubType { get; set; }

  [JsonPropertyName("initiating_symbol")]
  public string InitiatingSymbol { get; set; }

  [JsonPropertyName("initiating_original_cusip")]
  public string InitiatingCusip { get; set; }

  [JsonPropertyName("target_symbol")]
  public string TargetSymbol { get; set; }

  [JsonPropertyName("target_original_cusip")]
  public string TargetCusip { get; set; }

  [JsonPropertyName("declaration_date")]
  public DateOnly? DeclarationDate { get; set; }

  [JsonPropertyName("ex_date")]
  public DateOnly? ExecutionDate { get; set; }

  [JsonPropertyName("record_date")]
  public DateOnly? RecordDate { get; set; }

  [JsonPropertyName("payable_date")]
  public DateOnly? PayableDate { get; set; }

  [JsonPropertyName("cash")]
  public double? Cash { get; set; }

  [JsonPropertyName("old_rate")]
  public double? OldRate { get; set; }

  [JsonPropertyName("new_rate")]
  public double? NewRate { get; set; }
}
