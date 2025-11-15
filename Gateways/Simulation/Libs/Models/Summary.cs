using Core.Models;
using System.Collections.Generic;

namespace Simulation.Models
{
  public record Summary
  {
    /// <summary>
    /// Depth of market
    /// </summary>
    public Dom Dom { get; init; }

    /// <summary>
    /// Reference to the instrument
    /// </summary>
    public Instrument Instrument { get; init; }

    /// <summary>
    /// List of option contracts for the current point
    /// </summary>
    public List<Instrument> Options { get; init; } = [];
  }
}
