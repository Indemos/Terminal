using MudBlazor;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Enums;
using Terminal.Core.Models;
using Terminal.Records;

namespace Terminal.Components
{
  public partial class OrdersComponent
  {
    protected TableGroupDefinition<OrderRecord> GroupDefinition = new()
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
    protected virtual IList<OrderRecord> Items { get; set; } = [];

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="items"></param>
    public virtual Task UpdateItems(IEnumerable<OrderModel> items)
    {
      static OrderRecord getRecord(OrderModel o)
      {
        return new OrderRecord
        {
          Group = o.Transaction.Id,
          Time = o.Transaction.Time,
          Name = o.Transaction.Instrument.Name,
          Side = o.Side ?? OrderSideEnum.None,
          Size = o.Transaction.CurrentVolume ?? 0,
          Price = o.Transaction.Price ?? 0,
        };
      }

      Items = items.SelectMany(order =>
      {
        var subRecords = order
          .Orders
          .Where(o => o.Instruction is InstructionEnum.Side && o.Transaction is not null)
          .Select(o => getRecord(order));

        if (order.Transaction is not null)
        {
          subRecords = subRecords.Append(getRecord(order));
        }

        return subRecords;

      }).ToList();

      return Render();
    }

    /// <summary>
    /// Clear records
    /// </summary>
    public virtual void Clear() => Render(Items.Clear, false);
  }
}
