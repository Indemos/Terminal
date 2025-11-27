using Core.Models;
using System;

namespace Coin.Models
{
  public record Connection
  {
    /// <summary>
    /// Access token
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Streaming session token
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    /// Exchange
    /// </summary>
    public string Exchange { get; set; }

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
