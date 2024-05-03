using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonWatchList
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
  public List<JsonAsset> AssetsList { get; set; } = [];
}
