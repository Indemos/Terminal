namespace Core.Common.States
{
  public record InstrumentDescriptor : IDescriptor
  {
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
