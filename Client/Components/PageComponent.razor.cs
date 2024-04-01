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
    public bool IsConnection { get; set; }
    public bool IsSubscription { get; set; }
    public ChartsComponent ChartsView { get; set; }
    public ChartsComponent ReportsView { get; set; }
    public DealsComponent DealsView { get; set; }
    public OrdersComponent OrdersView { get; set; }
    public PositionsComponent PositionsView { get; set; }
    public StatementsComponent StatementsView { get; set; }
    public IGateway Adapter { get; set; }
    public Action Setup { get; set; }

    public async Task OnConnect()
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

    public void OnDisconnect()
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

    public void OnSubscribe()
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

    public void OnUnsubscribe()
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

    public void OnOpenStatements()
    {
      if (Adapter?.Account is not null)
      {
        StatementsView.UpdateItems(new[] { Adapter.Account });
      }
    }
  }
}
