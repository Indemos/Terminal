using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Messages;

public class WatchListMessage
{
  [JsonPropertyName("id")]
  public string WatchListId { get; set; }

  [JsonPropertyName("created_at")]
  public DateTime? CreatedUtc { get; set; }

  [JsonPropertyName("updated_at")]
  public DateTime? UpdatedUtc { get; set; }

  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("account_id")]
  public string AccountId { get; set; }

  [JsonPropertyName("assets")]
  public List<AssetMessage> AssetsList { get; set; } = [];
}
