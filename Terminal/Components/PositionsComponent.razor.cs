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
      static PositionRecord getRecord(string group, OrderModel o)
      {
        return new PositionRecord
        {
          Group = group,
          Time = o.Transaction.Time,
          Name = o.Transaction.Instrument.Name,
          Side = o.Side ?? OrderSideEnum.None,
          Size = o.Transaction.CurrentVolume ?? 0,
          OpenPrice = o.Price ?? 0,
          ClosePrice = o.GetCloseEstimate() ?? 0,
          Gain = o.GetGainEstimate() ?? 0
        };
      }

      Items = items.SelectMany((pos, i) =>
      {
        var group = $"{i + 1}";
        var record = getRecord(group, pos.Order);
        var subRecords = pos
          .Order
          .Orders
          .Where(o => Equals(o.Instruction, InstructionEnum.Side))
          .Select(o => getRecord(group, o));

        return subRecords.Concat([record]);

      }).ToList();

      return Render();
    }
  }
}
