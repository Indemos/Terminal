using System.Collections.Generic;

namespace Core.Common.States
{
  public record OrderGroupsResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public IList<DescriptorResponse> Data { get; init; } = [];
  }
}
