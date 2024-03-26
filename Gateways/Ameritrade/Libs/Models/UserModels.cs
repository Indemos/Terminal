using System;
using System.Text.Json.Serialization;

namespace Terminal.Gateway.Ameritrade.Models
{
  public struct UserModel
  {
    [JsonPropertyName("redirect_url")]
    public string RedirectUrl { get; set; }

    [JsonPropertyName("consumer_key")]
    public string ConsumerKey { get; set; }

    [JsonPropertyName("security_code")]
    public string SecurityCode { get; set; }

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("expiration_date")]
    public long ExpirationDate { get; set; }

    [JsonPropertyName("refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }
  }
}
