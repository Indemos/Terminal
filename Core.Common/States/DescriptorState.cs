namespace Core.Common.States
{
  public record DescriptorState
  {
    /// <summary>
    /// Namespace
    /// </summary>
    public string Space { get; init; }

    /// <summary>
    /// Order descriptor
    /// </summary>
    public string Order { get; init; }

    /// <summary>
    /// Account descriptor
    /// </summary>
    public string Account { get; init; }

    /// <summary>
    /// Order descriptor
    /// </summary>
    public string Instrument { get; init; }
  }
}
