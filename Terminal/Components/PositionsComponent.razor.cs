using MudBlazor;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Enums;
using Terminal.Core.Models;
using Terminal.Records;

namespace Terminal.Components
{
  public partial class PositionsComponent
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
    public virtual Task UpdateItems(IEnumerable<PositionModel> items)
    {
      static PositionRecord getRecord(OrderModel o)
      {
        return new PositionRecord
        {
          Time = o.Transaction.Time,
          Name = o.Transaction.Instrument.Name,
          Group = o.Transaction.Instrument.Basis?.Name ?? o.Transaction.Instrument.Name,
          Side = o.Side ?? OrderSideEnum.None,
          Size = o.Transaction.CurrentVolume ?? 0,
          OpenPrice = o.Price ?? 0,
          ClosePrice = o.GetPriceEstimate() ?? 0,
          Gain = o.GetGainEstimate() ?? 0
        };
      }

      Items = items.SelectMany(pos =>
      {
        var record = getRecord(pos.Order);
        var subRecords = pos
          .Order
          .Orders
          .Where(o => Equals(o.Instruction, InstructionEnum.Side))
          .Select(o => getRecord(o));

        return subRecords.Concat([record]);

      }).ToList();

      return Render();
    }
  }
}
