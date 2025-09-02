using Core.Common.States;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Common.Implementations
{
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
    Task<IIndicator> Update(IList<PriceState> collection);
  }

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
    public virtual Task<IIndicator> Update(IList<PriceState> collection) => default;
  }
}
