namespace Core.Common.States
{
  /// <summary>
  /// Grain name generator
  /// </summary>
  public interface IDescriptor
  {
  }

  public record BaseDescriptor : IDescriptor
  {
    /// <summary>
    /// Account descriptor
    /// </summary>
    public string Account { get; init; }
  }
}
