using Core.EnumSpace;
using FluentValidation;
using System;
using System.Drawing;

namespace Core.ModelSpace
{
  /// <summary>
  /// Generic model for time series
  /// </summary>
  public interface IChartModel : IBaseModel
  {
    /// <summary>
    /// Are of the chart where to display this data point
    /// </summary>
    string ChartArea { get; set; }

    /// <summary>
    /// Type of the chart to use for the data point
    /// </summary>
    string ChartType { get; set; }

    /// <summary>
    /// Primary color
    /// </summary>
    Color? PrimaryColor { get; set; }

    /// <summary>
    /// Secondary color, e.g. background
    /// </summary>
    Color? SecondaryColor { get; set; }

    /// <summary>
    /// Color for the ascending values
    /// </summary>
    Color? PositiveColor { get; set; }

    /// <summary>
    /// Color for the descending values
    /// </summary>
    Color? NegativeColor { get; set; }
  }

  /// <summary>
  /// Expando class that allows to extend other models in runtime
  /// </summary>
  public class ChartModel : BaseModel, IChartModel
  {
    /// <summary>
    /// Are of the chart where to display this data point
    /// </summary>
    public virtual string ChartArea { get; set; }

    /// <summary>
    /// Type of the chart to use for the data point
    /// </summary>
    public virtual string ChartType { get; set; }

    /// <summary>
    /// Primary color
    /// </summary>
    public virtual Color? PrimaryColor { get; set; }

    /// <summary>
    /// Secondary color, e.g. background
    /// </summary>
    public virtual Color? SecondaryColor { get; set; }

    /// <summary>
    /// Color for the ascending values
    /// </summary>
    public virtual Color? PositiveColor { get; set; }

    /// <summary>
    /// Color for the descending values
    /// </summary>
    public virtual Color? NegativeColor { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ChartModel()
    {
      NegativeColor = Color.Red;
      PositiveColor = Color.Green;
      PrimaryColor = Color.Black;
      SecondaryColor = Color.Empty;
      ChartType = nameof(ChartTypeEnum.Line);
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class ChartValidation : AbstractValidator<IChartModel>
  {
    public ChartValidation()
    {
      RuleFor(o => o.ChartArea).NotNull().NotEmpty().WithMessage("No chart area");
      RuleFor(o => o.ChartType).NotNull().NotEmpty().WithMessage("No chart type");
      RuleFor(o => o.Name).NotNull().NotEmpty().WithMessage("No series name");
    }
  }
}
