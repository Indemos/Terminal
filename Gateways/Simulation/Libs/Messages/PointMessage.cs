namespace Simulation.Messages
{
  using System.Collections.Generic;
  using Terminal.Core.Models;

  public partial class PointMessage
  {
    public PointModel Point { get; set; }

    public IList<OptionModel> Options { get; set; }
  }
}
