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
    protected IDictionary<string, ITransactionOrderModel> Items { get; set; } = new Dictionary<string, ITransactionOrderModel>();

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="items"></param>
    public Task UpdateItems(IDictionary<string, ITransactionOrderModel> items)
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
