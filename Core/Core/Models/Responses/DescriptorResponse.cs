namespace Core.Models
{
  public record DescriptorResponse : Response
  {
    /// <summary>
    /// Data
    /// </summary>
    public string Data { get; init; }
  }
}
