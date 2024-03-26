using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;

namespace Client.Components
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
    public virtual IGateway Adapter { get; set; }
    public virtual Action Setup { get; set; }

    public virtual async Task OnConnect()
    {
      try
      {
        OnDisconnect();
        Setup();

        IsConnection = true;
        IsSubscription = true;

        await Adapter.Connect();
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    public virtual void OnDisconnect()
    {
      try
      {
        Adapter?.Disconnect();

        ChartsView.Clear();
        ReportsView.Clear();

        IsConnection = false;
        IsSubscription = false;
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    public virtual void OnSubscribe()
    {
      try
      {
        IsSubscription = true;
        Adapter.Account.Instruments.ForEach(o => Adapter.Subscribe(o.Key));
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    public virtual void OnUnsubscribe()
    {
      try
      {
        IsSubscription = false;
        Adapter.Account.Instruments.ForEach(o => Adapter.Unsubscribe(o.Key));
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    public virtual void OnOpenStatements()
    {
      if (Adapter?.Account is not null)
      {
        StatementsView.UpdateItems(new[] { Adapter.Account });
      }
    }
  }
}
