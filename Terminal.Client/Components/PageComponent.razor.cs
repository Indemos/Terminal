using Distribution.DomainSpace;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using Terminal.Connector.Simulation;
using Terminal.Core.ServiceSpace;

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
    public virtual Adapter Gateway { get; set; }
    public virtual Action Setup { get; set; }

    public async Task OnConnect()
    {
      OnDisconnect();
      Setup();

      IsConnection = true;
      IsSubscription = true;

      await Gateway.Connect();
    }

    public void OnDisconnect()
    {
      IsConnection = false;
      IsSubscription = false;

      Gateway?.Disconnect();

      ChartsView.Clear();
      ReportsView.Clear();
    }

    public async Task OnSubscribe()
    {
      IsSubscription = true;

      await Gateway.Subscribe();
    }

    public void OnUnsubscribe()
    {
      IsSubscription = false;

      Gateway.Unsubscribe();
    }

    public void OnOpenStatements()
    {
      InstanceService<Scene>.Instance.Scheduler.Send(() =>
      {
        if (Gateway?.Account is not null)
        {
          StatementsView.UpdateItems(new[] { Gateway.Account });
        }
      });
    }
  }
}
