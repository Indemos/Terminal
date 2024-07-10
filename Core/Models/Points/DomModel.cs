using System.Collections.Generic;

namespace Terminal.Core.Models
{
  public class DomModel
  {
    public IList<PointModel> Asks { get; set; }
    public IList<PointModel> Bids { get; set; }
  }
}
