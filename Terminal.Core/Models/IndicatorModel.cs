using Terminal.Core.CollectionSpace;
using System;

namespace Terminal.Core.ModelSpace
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
    TOutput Calculate(IIndexCollection<TInput> collection);
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public class IndicatorModel<TInput, TOutput> : PointModel, IIndicator<TInput, TOutput> where TInput : IPointModel
  {
    /// <summary>
    /// Constructor
    /// </summary>
    public IndicatorModel()
    {
      Bar = new PointBarModel();
      Name = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Calculate indicator values
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public virtual TOutput Calculate(IIndexCollection<TInput> collection) => default;
  }
}
