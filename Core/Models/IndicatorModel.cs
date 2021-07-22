using Core.CollectionSpace;
using System;

namespace Core.ModelSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  public interface IIndicator<TInput, TOutput> : IPointModel where TInput : IPointModel
  {
    /// <summary>
    /// Calculate indicator values
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    TOutput Calculate(ITimeCollection<TInput> collection);
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public class IndicatorModel<TInput, TOutput> : PointModel, IIndicator<TInput, TOutput> where TInput : IPointModel
  {
    /// <summary>
    /// Internal indicator ID
    /// </summary>
    protected string _name = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Name
    /// </summary>
    public override string Name { get => _name; set => _name = value; }

    /// <summary>
    /// Constructor
    /// </summary>
    public IndicatorModel()
    {
      Bar = new PointBarModel();
    }

    /// <summary>
    /// Calculate indicator values
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public virtual TOutput Calculate(ITimeCollection<TInput> collection)
    {
      return default;
    }
  }
}
