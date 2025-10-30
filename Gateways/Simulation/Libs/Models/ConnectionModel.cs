using Core.Models;

namespace Simulation.Models
{
  public record ConnectionModel
  {
    /// <summary>
    /// Data source
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Account
    /// </summary>
    public AccountModel Account { get; init; }
  }
}
