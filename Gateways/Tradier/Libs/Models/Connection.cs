using Core.Models;
using System;

namespace Tradier.Models
{
  public record Connection
  {
    /// <summary>
    /// Access token
    /// </summary>
    public string AccessToken { get; init; }

    /// <summary>
    /// Streaming session token
    /// </summary>
    public string SessionToken { get; init; }

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
