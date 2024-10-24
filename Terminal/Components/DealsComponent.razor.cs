using MudBlazor;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Enums;
using Terminal.Core.Models;
using Terminal.Records;

namespace Terminal.Components
{
  public partial class DealsComponent
  {
    protected TableGroupDefinition<PositionRecord> GroupDefinition = new()
    {
      GroupName = "Group",
      Indentation = false,
      Expandable = true,
      IsInitiallyExpanded = true,
      Selector = (e) => e.Group
    };

    /// <summary>
    /// Table records
    /// </summary>
    protected virtual IList<PositionRecord> Items { get; set; } = [];

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="items"></param>
    public virtual void UpdateItems(IEnumerable<OrderModel> items)
    {
      Items = [.. items.Select(GetRecord)];
      InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Clear records
    /// </summary>
    public virtual void Clear() => UpdateItems([]);

    /// <summary>
    /// Map
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    private static PositionRecord GetRecord(OrderModel o)
    {
      return new PositionRecord
      {
        Time = o.Transaction.Time,
        Name = o.Transaction.Instrument.Name,
        Group = o.Transaction.Instrument.Basis?.Name ?? o.Transaction.Instrument.Name,
        Side = o.Side ?? OrderSideEnum.None,
        Size = o.Transaction.Volume ?? 0,
        OpenPrice = o.Price ?? 0,
        ClosePrice = o.Transaction.Price ?? 0,
        Gain = o.GetGainEstimate(o.Transaction.Price) ?? 0
      };
    }
  }
}
