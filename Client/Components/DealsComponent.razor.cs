using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Client.Records;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Client.Components
{
  public partial class DealsComponent
  {
    /// <summary>
    /// Table records
    /// </summary>
    protected IList<ActiveOrderRecord> Items { get; set; } = new List<ActiveOrderRecord>();

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="items"></param>
    public Task UpdateItems(ObservableCollection<PositionModel?> items)
    {
      Items = items.Select(o => new ActiveOrderRecord
      {
        Time = o?.Order?.Transaction?.Time,
        Name = o?.Order?.Transaction?.Instrument.Name,
        Side = o?.Order?.Side ?? OrderSideEnum.None,
        Size = o?.Order?.Transaction?.Volume ?? 0,
        OpenPrice = o?.Orders.First()?.Transaction?.Price ?? 0,
        ClosePrice = o?.Orders.Last()?.Transaction?.Price ?? 0,
        Gain = o?.GainLoss ?? 0

      }).ToList();

      return Render(() => { });
    }
  }
}
