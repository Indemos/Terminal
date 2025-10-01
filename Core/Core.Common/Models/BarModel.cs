namespace Core.Common.Models
{
  public record BarModel
  {
    /// <summary>
    /// Lowest price of the bar
    /// </summary>
    public double? Low { get; init; }

    /// <summary>
    /// Highest price of the bar
    /// </summary>
    public double? High { get; init; }

    /// <summary>
    /// Open price of the bar
    /// </summary>
    public double? Open { get; init; }

    /// <summary>
    /// Close price of the bar
    /// </summary>
    public double? Close { get; init; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public long? Time { get; init; }
  }
}
