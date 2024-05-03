using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Records;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Client.Components
{
  public partial class PositionsComponent
  {
    /// <summary>
    /// Table records
    /// </summary>
    protected virtual IList<ActiveOrderRecord> Items { get; set; } = new List<ActiveOrderRecord>();

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="items"></param>
    public virtual Task UpdateItems(IDictionary<string, PositionModel> items)
    {
      Items = items.Values.Select(o => new ActiveOrderRecord
      {
        Time = o.Order.Transaction.Time,
        Name = o.Order.Transaction.Instrument.Name,
        Side = o.Order.Side ?? OrderSideEnum.None,
        Size = o.Order.Transaction.Volume ?? 0,
        OpenPrice = o.Order.Transaction.Price ?? 0,
        ClosePrice = o.ClosePriceEstimate ?? 0,
        Gain = o.GainLossAverageEstimate ?? 0

      }).ToList();

      return Render(() => { });
    }
  }
}
