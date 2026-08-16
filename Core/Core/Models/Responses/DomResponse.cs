namespace Core.Models
{
  public record DomResponse : Response
  {
    /// <summary>
    /// Data
    /// </summary>
    public Dom Data { get; init; }
  }
}
