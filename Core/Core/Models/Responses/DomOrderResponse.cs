namespace Core.Models
{
  public record DomOrderResponse : Response
  {
    /// <summary>
    /// Data
    /// </summary>
    public DomOrder Data { get; init; }
  }
}
