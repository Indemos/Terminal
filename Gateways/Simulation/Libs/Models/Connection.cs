using Core.Models;

namespace Simulation.Models
{
  public record Connection
  {
    /// <summary>
    /// Data source
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Account
    /// </summary>
    public Account Account { get; init; }
  }
}
