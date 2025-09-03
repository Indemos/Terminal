namespace Core.Common.States
{
  public record OrderDescriptor : IDescriptor
  {
    /// <summary>
    /// Account descriptor
    /// </summary>
    public string Account { get; init; }

    /// <summary>
    /// Order descriptor
    /// </summary>
    public string Order { get; init; }
  }
}
