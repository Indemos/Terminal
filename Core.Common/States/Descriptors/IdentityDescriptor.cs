namespace Core.Common.States
{
  public record IdentityDescriptor : IDescriptor
  {
    /// <summary>
    /// Account descriptor
    /// </summary>
    public string Account { get; init; }

    /// <summary>
    /// Order descriptor
    /// </summary>
    public string Identity { get; init; }
  }
}
