namespace Core.Common.States
{
  /// <summary>
  /// Grain name generator
  /// </summary>
  public interface IDescriptor
  {
  }

  public record Descriptor : IDescriptor
  {
    /// <summary>
    /// Account descriptor
    /// </summary>
    public string Account { get; init; }
  }
}
