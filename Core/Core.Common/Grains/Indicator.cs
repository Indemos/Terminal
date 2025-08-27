using Core.Common.States;
using System.Collections.Generic;

namespace Core.Common.Grains
{
  /// <summary>
  /// Definition
  /// </summary>
  public interface IIndicator
  {
    /// <summary>
    /// Name
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Point
    /// </summary>
    PriceState Response { get; }

    /// <summary>
    /// Calculate indicator values
    /// </summary>
    /// <param name="collection"></param>
    IIndicator Update(IList<PriceState> collection);
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public class Indicator : IIndicator
  {
    /// <summary>
    /// Name
    /// </summary>
    public virtual string Name { get; set; }

    /// <summary>
    /// Point
    /// </summary>
    public virtual PriceState Response { get; protected set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Indicator() => Response = new();

    /// <summary>
    /// Calculate indicator values
    /// </summary>
    /// <param name="collection"></param>
    public virtual IIndicator Update(IList<PriceState> collection) => default;
  }
}
