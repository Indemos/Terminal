namespace Core.Models
{
  public record AccountResponse : Response
  {
    /// <summary>
    /// Data
    /// </summary>
    public Account Data { get; init; }
  }
}
