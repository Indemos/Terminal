using Distribution.Models;
using Distribution.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Services;

namespace Terminal.Components
{
  public partial class PageComponent
  {
    [Parameter] public virtual RenderFragment Header1 { get; set; } = default;
    [Parameter] public virtual RenderFragment Header2 { get; set; } = default;
    [Parameter] public virtual RenderFragment Header3 { get; set; } = default;
    [Parameter] public virtual RenderFragment Header4 { get; set; } = default;
    [Parameter] public virtual RenderFragment Header5 { get; set; } = default;
    [Parameter] public virtual RenderFragment Header6 { get; set; } = default;

    [Parameter] public virtual RenderFragment View1 { get; set; } = default;
    [Parameter] public virtual RenderFragment View2 { get; set; } = default;
    [Parameter] public virtual RenderFragment View3 { get; set; } = default;
    [Parameter] public virtual RenderFragment View4 { get; set; } = default;
    [Parameter] public virtual RenderFragment View5 { get; set; } = default;
    [Parameter] public virtual RenderFragment View6 { get; set; } = default;

    public virtual bool IsConnection { get; set; }
    public virtual bool IsSubscription { get; set; }
    public virtual ChartsComponent ChartsView { get; set; }
    public virtual ChartsComponent ReportsView { get; set; }
    public virtual DealsComponent DealsView { get; set; }
    public virtual OrdersComponent OrdersView { get; set; }
    public virtual PositionsComponent PositionsView { get; set; }
    public virtual StatementsComponent StatementsView { get; set; }
    public virtual Action OnPreConnect { get; set; } = () => { };
    public virtual Action OnPostConnect { get; set; } = () => { };
    public virtual Action OnDisconnect { get; set; } = () => { };
    public virtual IDictionary<string, IGateway> Adapters { get; set; } = new Dictionary<string, IGateway>();

    public virtual async Task Connect()
    {
      try
      {
        await Disconnect();

        OnPreConnect();

        await Task.WhenAll(Adapters.Values.Select(o => o.Connect()));

        IsConnection = true;
        IsSubscription = true;

        OnPostConnect();
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
    }

    public virtual async Task Disconnect()
    {
      try
      {
        var options = new OptionModel { IsRemovable = false };

        await Task.WhenAll(Adapters.Values.Select(o => o.Disconnect()));

        InstanceService<ScheduleService>.Instance.Send(() =>
        {
          ChartsView.Clear();
          ReportsView.Clear();
          DealsView.Clear();
          OrdersView.Clear();
          PositionsView.Clear();

          OnDisconnect();

        }, options);

        IsConnection = false;
        IsSubscription = false;

      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
    }

    public virtual async Task Subscribe()
    {
      try
      {
        await Task.WhenAll(Adapters
          .Values
          .SelectMany(adapter => adapter
            .Account
            .Instruments
            .Values
            .Select(o => adapter.Subscribe(o))));

        IsSubscription = true;
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
    }

    public virtual async Task Unsubscribe()
    {
      try
      {
        await Task.WhenAll(Adapters
          .Values
          .SelectMany(adapter => adapter
            .Account
            .Instruments
            .Values
            .Select(o => adapter.Unsubscribe(o))));

        IsSubscription = false;
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
    }

    public virtual void OpenState()
    {
      StatementsView.UpdateItems(Adapters.Values.Select(o => o.Account).ToList());
    }
  }
}
