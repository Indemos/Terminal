using Core.Models;
using System;

namespace Tradier.Models
{
  public record Connection
  {
    /// <summary>
    /// Access token
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Streaming session token
    /// </summary>
    public string SessionToken { get; set; }

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
