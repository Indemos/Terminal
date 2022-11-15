using Distribution.DomainSpace;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Client.Records;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;
using Terminal.Core.ServiceSpace;

namespace Terminal.Client.Components
{
  public partial class OrdersComponent
  {
    /// <summary>
    /// Table records
    /// </summary>
    protected IList<OrderRecord> Items { get; set; } = new List<OrderRecord>();

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="items"></param>
    public Task UpdateItems(IDictionary<string, ITransactionOrderModel> items)
    {
      Items = items.Values.Select(o => new OrderRecord
      {
        Time = o.Time,
        Name = o.Instrument.Name,
        Side = o.Side ?? OrderSideEnum.None,
        Size = o.Size ?? 0,
        Price = o.Price ?? 0,

      }).ToList();

      return InvokeAsync(StateHasChanged);
    }
  }
}
