using Core.Models;
using Core.Services;
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
    Price Response { get; }

    /// <summary>
    /// Calculate indicator values
    /// </summary>
    /// <param name="collection"></param>
    Task<IIndicator> Update(IList<Price> collection);
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
    public virtual Price Response { get; protected set; } = new();

    /// <summary>
    /// Average
    /// </summary>
    public virtual AverageService Average { get; protected set; } = new();

    /// <summary>
    /// Calculate indicator values
    /// </summary>
    /// <param name="collection"></param>
    public virtual Task<IIndicator> Update(IList<Price> collection) => Task.FromResult(this as IIndicator);
  }
}
