using SkiaSharp;

namespace Terminal.Core.ModelSpace
{
  public interface IViewModel : IBaseModel
  {
    /// <summary>
    /// Area
    /// </summary>
    string Area { get; set; }

    /// <summary>
    /// Type of the chart to use for the data point
    /// </summary>
    string Shape { get; set; }

    /// <summary>
    /// Color
    /// </summary>
    SKColor? Color { get; set; }
  }

  public class ViewModel : BaseModel, IViewModel
  {
    /// <summary>
    /// Area
    /// </summary>
    public virtual string Area { get; set; }

    /// <summary>
    /// Type of the chart to use for the data point
    /// </summary>
    public virtual string Shape { get; set; }

    /// <summary>
    /// Color
    /// </summary>
    public virtual SKColor? Color { get; set; }
  }
}
