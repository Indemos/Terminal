using System;
using System.Collections.Generic;

namespace Terminal.Core.Models
{
  public class OrdersArgs
  {
    /// <summary>
    /// Order status
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Symbols
    /// </summary>
    public IList<string> Names { get; set; }
  }
}
