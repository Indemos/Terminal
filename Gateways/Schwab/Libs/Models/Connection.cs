using Core.Models;
using System;

namespace Schwab.Models
{
  public record Connection
  {
    /// <summary>
    /// Client ID
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Client secret
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    /// Access token
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Refresh token
    /// </summary>
    public string RefreshToken { get; set; }

    /// <summary>
    /// Timeout
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Account
    /// </summary>
    public Account Account { get; init; }
  }
}
