using Orleans;

namespace Core.Common.States
{
  public record ErrorState
  {
    public string Message { get; init; }
  }
}
