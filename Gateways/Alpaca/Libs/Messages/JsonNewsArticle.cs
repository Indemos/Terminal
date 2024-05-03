using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alpaca.Markets;

public class JsonNewsArticle
{
  public class Image
  {
    [JsonPropertyName("size")]
    public string Size { get; set; }

    [JsonPropertyName("url")]
    public Uri Url { get; set; }
  }

  [JsonPropertyName("id")]
  public double? Id { get; set; }

  [JsonPropertyName("headline")]
  public string Headline { get; set; }

  [JsonPropertyName("created_at")]
  public DateTime? CreatedAtUtc { get; set; }

  [JsonPropertyName("updated_at")]
  public DateTime? UpdatedAtUtc { get; set; }

  [JsonPropertyName("author")]
  public string Author { get; set; }

  [JsonPropertyName("summary")]
  public string Summary { get; set; }

  [JsonPropertyName("content")]
  public string Content { get; set; }

  [JsonPropertyName("url")]
  public Uri ArticleUrl { get; set; }

  [JsonPropertyName("source")]
  public string Source { get; set; }

  [JsonPropertyName("symbols")]
  public List<string> SymbolsList { get; set; } = [];

  [JsonPropertyName("images")]
  public List<Image> Images { get; set; } = [];

  [JsonIgnore]
  public Uri SmallImageUrl { get; set; }

  [JsonIgnore]
  public Uri LargeImageUrl { get; set; }

  [JsonIgnore]
  public Uri ThumbImageUrl { get; set; }
}
