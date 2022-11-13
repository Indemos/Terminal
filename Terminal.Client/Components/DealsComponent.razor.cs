using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Client.Records;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Client.Components
{
  public partial class DealsComponent 
  {
    /// <summary>
    /// Syncer
    /// </summary>
    protected Task Updater { get; set; }

    /// <summary>
    /// Table records
    /// </summary>
    protected IList<ActiveOrderRecord> Items { get; set; } = new List<ActiveOrderRecord>();

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="items"></param>
    public Task UpdateItems(IEnumerable<ITransactionPositionModel> items)
    {
      if (Updater?.IsCompleted is false)
      {
        return Updater;
      }

      Items = items.Select(o => new ActiveOrderRecord
      {
        Time = o.Time,
        Name = o.Instrument.Name,
        Side = o.Side ?? OrderSideEnum.None,
        Size = o.Size ?? 0,
        OpenPrice = o.OpenPrice ?? 0,
        ClosePrice = o.ClosePriceEstimate ?? 0,
        Gain = o.GainLossAverageEstimate ?? 0

      }).ToList();

      return Updater = InvokeAsync(StateHasChanged);
    }
  }
}
