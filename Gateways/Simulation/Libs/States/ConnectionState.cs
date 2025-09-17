using Core.Common.States;

namespace Simulation.States
{
  public record ConnectionState : AccountState
  {
    /// <summary>
    /// Speed in microsecons
    /// </summary>
    public int Speed { get; init; }

    /// <summary>
    /// Data source
    /// </summary>
    public string Source { get; init; }
  }
}
