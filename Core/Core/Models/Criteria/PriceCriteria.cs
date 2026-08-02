namespace Core.Models
{
  public record PriceCriteria : Criteria
  {
    /// <summary>
    /// Duration
    /// </summary>
    public int? Frame { get; init; }

    /// <summary>
    /// Duration
    /// </summary>
    public int? Duration { get; init; }

    /// <summary>
    /// Price type - bars, trades, ticks
    /// </summary>
    public string PriceType { get; init; }

    /// <summary>
    /// Period type - day, hour, minute
    /// </summary>
    public string FrameType { get; init; }

    /// <summary>
    /// Duration type - year, month, day
    /// </summary>
    public string DurationType { get; init; }
  }
}
