using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Client.Records;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Client.Components
{
  public partial class PositionsComponent
  {
    /// <summary>
    /// Table records
    /// </summary>
    protected IList<ActiveOrderRecord> Items { get; set; } = new List<ActiveOrderRecord>();

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="items"></param>
    public Task UpdateItems(IDictionary<string, ITransactionPositionModel> items)
    {
      return Render(() =>
      {
        Items = items.Values.Select(o => new ActiveOrderRecord
        {
          Time = o.Time,
          Name = o.Instrument.Name,
          Side = o.Side ?? OrderSideEnum.None,
          Size = o.Volume ?? 0,
          OpenPrice = o.OpenPrice ?? 0,
          ClosePrice = o.ClosePriceEstimate ?? 0,
          Gain = o.GainLossAverageEstimate ?? 0

        }).ToList();
      });
    }
  }
}
