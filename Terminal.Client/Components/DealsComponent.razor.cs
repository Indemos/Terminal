using Terminal.Core.ModelSpace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Terminal.Client.Components
{
  public partial class DealsComponent : IDisposable
  {
    /// <summary>
    /// Table headers
    /// </summary>
    protected IEnumerable<string> Columns = new List<string>();

    /// <summary>
    /// Table records
    /// </summary>
    protected IList<ITransactionPositionModel> Items = new List<ITransactionPositionModel>();

    /// <summary>
    /// Component load
    /// </summary>
    /// <returns></returns>
    protected override Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        Columns = new List<string>
        {
          "Time",
          "Instrument",
          "Size",
          "Side",
          "Open Price",
          "Close Price",
          "PnL"
        };

        CreateItems(new AccountModel[0]);
      }

      return base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// Generate table records 
    /// </summary>
    /// <param name="accounts"></param>
    protected Task CreateItems(IEnumerable<IAccountModel> accounts)
    {
      Items = accounts.SelectMany(account => account.Positions).ToList();

      return InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
    }
  }
}
