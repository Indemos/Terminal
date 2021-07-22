using Core.ModelSpace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Presentation.Components
{
  public partial class OrderComponent : IDisposable
  {
    /// <summary>
    /// Subscription controller
    /// </summary>
    protected ISubject<bool> _subscriptions = new Subject<bool>();

    /// <summary>
    /// Table headers
    /// </summary>
    protected IEnumerable<string> _columns = new List<string>();

    /// <summary>
    /// Table records
    /// </summary>
    protected IList<dynamic> _items = new List<dynamic>();

    /// <summary>
    /// Component load
    /// </summary>
    /// <returns></returns>
    protected override async Task OnInitializedAsync()
    {
      _columns = new List<string>
      {
        "Time",
        "Instrument",
        "Size",
        "Side",
        "Open Price"
      };

      await CreateSubscriptions();
    }

    /// <summary>
    /// Initialize subscriptions
    /// </summary>
    /// <returns></returns>
    protected async Task CreateSubscriptions()
    {
      var processors = InstanceManager<ResponseModel<IProcessorModel>>.Instance.Items;
      var accounts = processors.SelectMany(processor => processor.Gateways.Select(o => o.Account));

      accounts
        .Select(account => account.ActiveOrders.CollectionStream)
        .Merge()
        .TakeUntil(_subscriptions)
        .Subscribe(async message => await CreateItems(accounts));

      await CreateItems(accounts);
    }

    /// <summary>
    /// Generate table records 
    /// </summary>
    /// <param name="accounts"></param>
    protected async Task CreateItems(IEnumerable<IAccountModel> accounts)
    {
      var orders = accounts.SelectMany(account => account.ActiveOrders);

      _items.Clear();

      foreach (var order in orders)
      {
        var item = new ExpandoModel();

        item["Time"] = order.Time;
        item["Side"] = order.Type;
        item["Instrument"] = order.Instrument.Name;
        item["Size"] = string.Format("{0:0.00###}", order.Size);
        item["Open Price"] = string.Format("{0:0.00###}", order.Price);

        _items.Add(item);
      }

      await InvokeAsync(() => StateHasChanged());
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
      _subscriptions.OnNext(true);
    }
  }
}
