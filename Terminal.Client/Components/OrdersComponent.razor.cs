using Terminal.Core.CollectionSpace;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Terminal.Client.Components
{
  public partial class OrdersComponent : IDisposable
  {
    /// <summary>
    /// Table headers
    /// </summary>
    protected IEnumerable<string> Columns { get; set; }

    /// <summary>
    /// Table records
    /// </summary>
    protected IList<ITransactionOrderModel> Items { get; set; }

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
          "Open Price"
        };

        Items = new List<ITransactionOrderModel>();

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
      Items = accounts.SelectMany(account => account.ActiveOrders).ToList();

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
