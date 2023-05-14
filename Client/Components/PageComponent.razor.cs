using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Schedule.RunnerSpace;
using System;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Services;

namespace Terminal.Client.Components
{
    public partial class PageComponent
  {
    [Inject] IConfiguration Configuration { get; set; }

    /// <summary>
    /// Controls
    /// </summary>
    public virtual bool IsConnection { get; set; }
    public virtual bool IsSubscription { get; set; }
    public virtual ChartsComponent ChartsView { get; set; }
    public virtual ChartsComponent ReportsView { get; set; }
    public virtual DealsComponent DealsView { get; set; }
    public virtual OrdersComponent OrdersView { get; set; }
    public virtual PositionsComponent PositionsView { get; set; }
    public virtual StatementsComponent StatementsView { get; set; }
    public virtual IConnector Adapter { get; set; }
    public virtual Action Setup { get; set; }

    public virtual async Task OnConnect()
    {
      OnDisconnect();
      Setup();

      IsConnection = true;
      IsSubscription = true;

      await Adapter.Connect();
    }

    public virtual void OnDisconnect()
    {
      IsConnection = false;
      IsSubscription = false;

      Adapter?.Disconnect();

      ChartsView.Clear();
      ReportsView.Clear();
    }

    public virtual async Task OnSubscribe()
    {
      IsSubscription = true;

      await Adapter.Subscribe();
    }

    public virtual void OnUnsubscribe()
    {
      IsSubscription = false;

      Adapter.Unsubscribe();
    }

    public virtual void OnOpenStatements()
    {
      InstanceService<BackgroundRunner>.Instance.Send(() =>
      {
        if (Adapter?.Account is not null)
        {
          StatementsView.UpdateItems(new[] { Adapter.Account });
        }
      });
    }
  }
}
