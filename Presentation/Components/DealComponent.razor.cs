using Core.ModelSpace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Presentation.Components
{
  public partial class DealComponent : IDisposable
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
        "Open Price",
        "Close Price",
        "PnL"
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
        .Select(account => account.Positions.CollectionStream)
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
      var positions = accounts.SelectMany(account => account.Positions);

      _items.Clear();

      foreach (var position in positions)
      {
        var item = new ExpandoModel();

        item["Time"] = position.Time;
        item["Side"] = position.Type;
        item["Instrument"] = position.Instrument.Name;
        item["Size"] = string.Format("{0:0.00###}", position.Size);
        item["PnL"] = string.Format("{0:0.00###}", position.GainLoss);
        item["Open Price"] = string.Format("{0:0.00###}", position.Price);
        item["Close Price"] = string.Format("{0:0.00###}", position.ClosePrice);

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
