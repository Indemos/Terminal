using Core.Models;
using System;

namespace Schwab.Models
{
  public record Connection
  {
    /// <summary>
    /// Client ID
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Client secret
    /// </summary>
    public string Secret { get; init; }

    /// <summary>
    /// Access token
    /// </summary>
    public string AccessToken { get; init; }

    /// <summary>
    /// Refresh token
    /// </summary>
    public string RefreshToken { get; init; }

    /// <summary>
    /// Timeout
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Account
    /// </summary>
    public Account Account { get; init; }
  }
}
