namespace Core.Models
{
  public record PriceResponse : Response
  {
    /// <summary>
    /// Data
    /// </summary>
    public Price Data { get; init; }
  }
}
