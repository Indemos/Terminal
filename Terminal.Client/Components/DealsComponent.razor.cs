using System.Collections.Generic;
using System.Threading.Tasks;
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
    protected IEnumerable<ITransactionPositionModel> Items { get; set; } = new List<ITransactionPositionModel>();

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

      Items = items;

      return Updater = InvokeAsync(StateHasChanged);
    }
  }
}
