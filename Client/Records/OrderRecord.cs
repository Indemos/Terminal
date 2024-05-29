using System;
using Terminal.Core.Enums;

namespace Client.Records
{
  /// <summary>
  /// Template record
  /// </summary>
  public struct OrderRecord
  {
    public string Name { get; set; }
    public decimal Size { get; set; }
    public decimal Price { get; set; }
    public DateTime? Time { get; set; }
    public OrderSideEnum? Side { get; set; }
  }
}
