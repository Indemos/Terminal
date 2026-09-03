namespace Core.Models
{
  public record InstrumentResponse : Response
  {
    /// <summary>
    /// Data
    /// </summary>
    public Instrument Data { get; init; }
  }
}
