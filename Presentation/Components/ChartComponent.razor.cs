using Core.EnumSpace;
using Core.ModelSpace;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Presentation.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Presentation.Components
{
  public partial class ChartComponent : IDisposable
  {
    [Inject]
    private IJSRuntime _scriptRuntime { get; set; }

    [Inject]
    private CommandService _commandService { get; set; }

    [Parameter]
    public string Id { get; set; } = "chartContainer";

    /// <summary>
    /// Subscription controller
    /// </summary>
    protected ISubject<bool> _subscriptions = new Subject<bool>();

    /// <summary>
    /// Chart areas
    /// </summary>
    protected IDictionary<string, bool> _charts = new Dictionary<string, bool>();

    /// <summary>
    /// Page load event processor
    /// </summary>
    /// <param name="setup"></param>
    /// <returns></returns>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        _subscriptions.OnNext(false);

        var processors = InstanceManager<ResponseModel<IProcessorModel>>
          .Instance
          .Items;

        processors
          .Select(o => o.StateStream)
          .Merge()
          .TakeUntil(_subscriptions)
          .Subscribe(async message =>
          {
            if (Equals(message, StatusEnum.Active))
            {
              _charts = InstanceManager<ResponseModel<IProcessorModel>>
                .Instance
                .Items
                .SelectMany(o => o.Charts)
                .GroupBy(o => o.ChartArea)
                .ToDictionary(o => o.Key, o => false);

              await InvokeAsync(() => StateHasChanged());
              await Task.Delay(1);
              await CreateSubscriptions();
            }

            if (Equals(message, StatusEnum.Inactive))
            {
              await _commandService.DeleteCharts(_scriptRuntime, Id);
            }
          });
      }

      await Task.FromResult(0);
    }

    /// <summary>
    /// Initialize data and order subscriptions
    /// </summary>
    /// <returns></returns>
    protected async Task CreateSubscriptions()
    {
      var processors = InstanceManager<ResponseModel<IProcessorModel>>.Instance.Items;
      var charts = processors.SelectMany(o => o.Charts);
      var areaGroups = charts.GroupBy(o => o.ChartArea).ToDictionary(o => o.Key, o => o.ToList());
      var seriesGroups = charts.GroupBy(o => o.Name).ToDictionary(o => o.Key, o => o.ToList());
      var gateways = processors.SelectMany(processor => processor.Gateways);

      await _commandService.CreateCharts(_scriptRuntime, Id, charts);

      gateways
        .SelectMany(gateway => gateway.Account.Instruments.Values.Select(instrument => instrument.PointGroups.ItemStream))
        .Merge()
        .TakeUntil(_subscriptions)
        .Subscribe(async message =>
        {
          if (Equals(message.Action, ActionEnum.Create) || Equals(message.Action, ActionEnum.Update))
          {
            message.Next.Series[message.Next.Name] = message.Next;

            var series = message
              .Next
              .Series
              .Values
              .Where(o => areaGroups.ContainsKey(o.Chart.ChartArea) && seriesGroups.ContainsKey(o.Chart.Name));

            if (series.Any())
            {
              await _commandService.UpdatePoints(_scriptRuntime, series);
            }
          }
        });

      gateways
        .Select(gateway => gateway.Account.ActiveOrders.CollectionStream)
        .Merge()
        .TakeUntil(_subscriptions)
        .Subscribe(async message =>
        {
          var orders = message
            .Next
            .Where(o => areaGroups.ContainsKey(o.Instrument.Chart.ChartArea) && seriesGroups.ContainsKey(o.Instrument.Chart.Name));

          if (orders.Any())
          {
            await _commandService.UpdateLevels(_scriptRuntime, orders);
          }
        });

      gateways
        .Select(gateway => gateway.Account.Orders.ItemStream)
        .Merge()
        .TakeUntil(_subscriptions)
        .Subscribe(async message =>
        {
          var order = message.Next ?? message.Previous;

          if (Equals(message.Action, ActionEnum.Create))
          {
            var instrument = message
              .Next
              .Instrument;

            var chartModel = new ChartModel
            {
              ChartArea = instrument.Chart.ChartArea,
              ChartType = nameof(ChartTypeEnum.Deal),
              Name = nameof(NameEnum.Transactions) + " : " + instrument.Name
            };

            if (areaGroups.ContainsKey(chartModel.ChartArea) && seriesGroups.ContainsKey(chartModel.Name))
            {
              await _commandService.UpdateTransactions(_scriptRuntime, chartModel, new[] { message.Next });
            }
          }
        });
    }

    /// <summary>
    /// Expand or collapse selected area
    /// </summary>
    /// <param name="area"></param>
    protected void ExpandChart(string area)
    {
      _charts[area] = !_charts[area];
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
