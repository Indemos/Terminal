namespace Terminal.Core.ModelSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  public interface IPointBarModel : IBaseModel
  {
    /// <summary>
    /// Lowest price of the bar
    /// </summary>
    double? Low { get; set; }

    /// <summary>
    /// Highest price of the bar
    /// </summary>
    double? High { get; set; }

    /// <summary>
    /// Open price of the bar
    /// </summary>
    double? Open { get; set; }

    /// <summary>
    /// Close price of the bar
    /// </summary>
    double? Close { get; set; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public class PointBarModel : BaseModel, IPointBarModel
  {
    /// <summary>
    /// Lowest price of the bar
    /// </summary>
    public virtual double? Low { get; set; }

    /// <summary>
    /// Highest price of the bar
    /// </summary>
    public virtual double? High { get; set; }

    /// <summary>
    /// Open price of the bar
    /// </summary>
    public virtual double? Open { get; set; }

    /// <summary>
    /// Close price of the bar
    /// </summary>
    public virtual double? Close { get; set; }
  }
}
