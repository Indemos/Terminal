using System.Collections.Generic;
using System.Threading.Tasks;
using Terminal.Core.ModelSpace;

namespace Terminal.Client.Components
{
  public partial class OrdersComponent
  {
    /// <summary>
    /// Syncer
    /// </summary>
    protected Task Updater { get; set; }

    /// <summary>
    /// Table records
    /// </summary>
    protected IEnumerable<ITransactionOrderModel> Items { get; set; } = new List<ITransactionOrderModel>();

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="items"></param>
    public Task UpdateItems(IEnumerable<ITransactionOrderModel> items)
    {
      if (Updater?.IsCompleted is false)
      {
        return Updater;
      }

      Items = items;

      return Updater = InvokeAsync(StateHasChanged);
    }
  }
}
