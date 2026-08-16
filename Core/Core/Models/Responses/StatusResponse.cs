using Core.Enums;

namespace Core.Models
{
  public record StatusResponse : Response
  {
    /// <summary>
    /// Data
    /// </summary>
    public StatusEnum? Data { get; init; }
  }
}
