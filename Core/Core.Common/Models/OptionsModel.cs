using System.Collections.Generic;

namespace Core.Common.Models
{
  public record OptionsModel
  {
    /// <summary>
    /// List of option contracts for the current point
    /// </summary>
    public IList<InstrumentModel> Options { get; init; } = [];
  }
}
