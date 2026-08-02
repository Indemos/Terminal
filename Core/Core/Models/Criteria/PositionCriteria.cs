namespace Core.Models
{
  public record PositionCriteria : Criteria
  {
    /// <summary>
    /// Instrument name
    /// </summary>
    public string Name { get; init; }
  }
}
