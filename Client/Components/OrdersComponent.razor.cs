using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Client.Records;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Client.Components
{
  public partial class OrdersComponent
  {
    /// <summary>
    /// Table records
    /// </summary>
    protected virtual IList<OrderRecord> Items { get; set; } = new List<OrderRecord>();

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="items"></param>
    public virtual Task UpdateItems(IDictionary<string, OrderModel> items)
    {
      Items = items.Values.Select(o => new OrderRecord
      {
        Time = o.Transaction.Time,
        Name = o.Transaction.Instrument.Name,
        Side = o.Side ?? OrderSideEnum.None,
        Size = o.Transaction.Volume ?? 0,
        Price = o.Transaction.Price ?? 0,

      }).ToList();

      return Render(() => { });
    }
  }
}
