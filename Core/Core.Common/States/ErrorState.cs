using Orleans;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record ErrorState
  {
    [Id(0)] public string Message { get; init; }
  }
}
