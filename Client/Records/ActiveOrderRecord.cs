using System;
using Terminal.Core.Enums;

namespace Client.Records
{
  /// <summary>
  /// Template record
  /// </summary>
  public struct ActiveOrderRecord
  {
    public string Name { get; set; }
    public double Size { get; set; }
    public double Gain { get; set; }
    public double OpenPrice { get; set; }
    public double ClosePrice { get; set; }
    public DateTime? Time { get; set; }
    public OrderSideEnum? Side { get; set; }
  }
}
