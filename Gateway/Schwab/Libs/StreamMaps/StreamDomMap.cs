using System.Collections.Generic;

namespace Schwab.Mappers
{
  public class StreamDomMap
  {
    public static IDictionary<string, string> Map { get; set; } = new Dictionary<string, string>
    {
      {"Symbol", "0"},
      {"Market Snapshot Time", "1"},
      {"Bid Side Levels", "2"},
      {"Ask Side Levels", "3"}
    };
  }
}
