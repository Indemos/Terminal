using Core.Models;

namespace Schwab.Models
{
  public record ConnectionModel
  {
    /// <summary>
    /// Client ID
    /// </summary>
    public virtual string Id { get; set; }

    /// <summary>
    /// Client secret
    /// </summary>
    public virtual string Secret { get; set; }

    /// <summary>
    /// Access token
    /// </summary>
    public virtual string AccessToken { get; set; }

    /// <summary>
    /// Refresh token
    /// </summary>
    public virtual string RefreshToken { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    public AccountModel Account { get; init; }
  }
}
