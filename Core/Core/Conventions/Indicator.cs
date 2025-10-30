using Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Conventions
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
    PriceModel Response { get; }

    /// <summary>
    /// Calculate indicator values
    /// </summary>
    /// <param name="collection"></param>
    Task<IIndicator> Update(IList<PriceModel> collection);
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
    public virtual PriceModel Response { get; protected set; } = new();

    /// <summary>
    /// Calculate indicator values
    /// </summary>
    /// <param name="collection"></param>
    public virtual Task<IIndicator> Update(IList<PriceModel> collection) => Task.FromResult(this as IIndicator);
  }
}
