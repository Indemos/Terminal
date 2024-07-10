using Distribution.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Services;

namespace Client.Components
{
  public partial class PageComponent
  {
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
    public virtual Action OnPreConnect { get; set; }
    public virtual Action OnPostConnect { get; set; }

    public virtual async Task Connect()
    {
      try
      {
        Disconnect();
        OnPreConnect();

        IsConnection = true;
        IsSubscription = true;

        await Adapter.Connect();

        OnPostConnect();
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
    }

    public virtual void Disconnect()
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
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
    }

    public virtual void Subscribe()
    {
      try
      {
        IsSubscription = true;
        Adapter.Account.Instruments.ForEach(o => Adapter.Subscribe(o.Value));
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
    }

    public virtual void Unsubscribe()
    {
      try
      {
        IsSubscription = false;
        Adapter.Account.Instruments.ForEach(o => Adapter.Unsubscribe(o.Value));
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
    }

    public virtual void OpenState()
    {
      if (Adapter?.Account is not null)
      {
        StatementsView.UpdateItems([Adapter.Account]);
      }
    }
  }
}
