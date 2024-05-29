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
    public decimal Size { get; set; }
    public decimal Gain { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public DateTime? Time { get; set; }
    public OrderSideEnum? Side { get; set; }
  }
}
