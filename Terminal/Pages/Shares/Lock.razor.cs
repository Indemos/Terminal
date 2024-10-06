using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Simulation;
using SkiaSharp;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Components;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;

namespace Terminal.Pages.Shares
{
  public partial class Lock
  {
    [Inject] IConfiguration Configuration { get; set; }

    /// <summary>
    /// Strategy
    /// </summary>
    const string _assetX = "GOOG";
    const string _assetY = "GOOGL";

    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await CreateViews();

        View.OnPreConnect = CreateAccounts;
        View.OnPostConnect = () =>
        {
          var account = View.Adapters["Sim"].Account;

          View.DealsView.UpdateItems(account.Deals);
          View.OrdersView.UpdateItems(account.Orders.Values);
          View.PositionsView.UpdateItems(account.Positions.Values);
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    protected virtual async Task CreateViews()
    {
      var indAreas = new Shape();
      var indCharts = new Shape();

      indCharts.Groups["X"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      indCharts.Groups["Y"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      indAreas.Groups["Prices"] = indCharts;

      await View.ChartsView.Create(indAreas);

      var pnlGain = new ComponentModel { Color = SKColors.OrangeRed, Size = 2 };
      var pnlBalance = new ComponentModel { Color = SKColors.Black };
      var pnlAreas = new Shape();
      var pnlCharts = new Shape();

      pnlCharts.Groups["Balance"] = new AreaShape { Component = pnlBalance };
      pnlCharts.Groups["PnL"] = new LineShape { Component = pnlGain };
      pnlAreas.Groups["Performance"] = pnlCharts;

      await View.ReportsView.Create(pnlAreas);
    }

    protected virtual void CreateAccounts()
    {
      var account = new Account
      {
        Balance = 25000,
        Instruments = new ConcurrentDictionary<string, InstrumentModel>
        {
          [_assetX] = new InstrumentModel { Name = _assetX },
          [_assetY] = new InstrumentModel { Name = _assetY }
        }
      };

      View.Adapters["Sim"] = new Adapter
      {
        Speed = 1,
        Account = account,
        Source = Configuration["Simulation:Source"]
      };

      Performance = new PerformanceIndicator { Name = "Balance" };

      View
        .Adapters
        .Values
        .ForEach(adapter => adapter.PointStream += async message => await OnData(message.Next));
    }

    protected async Task OnData(PointModel point)
    {
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
      var instrumentX = account.Instruments[_assetX];
      var instrumentY = account.Instruments[_assetY];
      var seriesX = instrumentX.Points;
      var seriesY = instrumentY.Points;

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var pointX = seriesX.Last().Last;
      var pointY = seriesY.Last().Last;
      var performance = Performance.Calculate([account]);

      if (account.Positions.Count is 0)
      {
        await OpenPositions(100, 90);
      }

      if (account.Positions.Count > 0)
      {
        if (performance.Point.Last - account.Balance > 5)
        {
          await ClosePositions();
        }
      }

      View.DealsView.UpdateItems(account.Deals);
      View.OrdersView.UpdateItems(account.Orders.Values);
      View.PositionsView.UpdateItems(account.Positions.Values);
      View.ChartsView.UpdateItems(
      [
        KeyValuePair.Create("X", new PointModel { Time = point.Time, Last = pointX }),
        KeyValuePair.Create("Y", new PointModel { Time = point.Time, Last = -pointY }),
      ]);

      View.ReportsView.UpdateItems(
      [
        KeyValuePair.Create("Balance", new PointModel { Time = point.Time, Last = account.Balance }),
        KeyValuePair.Create("PnL", new PointModel { Time = point.Time, Last = performance.Point.Last })
      ]);
    }

    protected async Task OpenPositions(double? amountX, double? amountY)
    {
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
      var instrumentX = account.Instruments[_assetX];
      var instrumentY = account.Instruments[_assetY];

      await ClosePositions();
      await adapter.CreateOrders(
      [
        new OrderModel
        {
          Side = OrderSideEnum.Buy,
          Type = OrderTypeEnum.Market,
          Transaction = new() { Volume = amountX, Instrument = instrumentX }
        },
        new OrderModel
        {
          Side = OrderSideEnum.Sell,
          Type = OrderTypeEnum.Market,
          Transaction = new() { Volume = amountY, Instrument = instrumentY }
        }
      ]);
    }

    protected async Task ClosePositions()
    {
      var adapter = View.Adapters["Sim"];

      foreach (var position in adapter.Account.Positions.ToList())
      {
        var side = OrderSideEnum.Buy;

        if (position.Value.Side is OrderSideEnum.Buy)
        {
          side = OrderSideEnum.Sell;
        }

        var order = new OrderModel
        {
          Side = side,
          Type = OrderTypeEnum.Market,
          Transaction = new()
          {
            Volume = position.Value.Transaction.Volume,
            Instrument = position.Value.Transaction.Instrument
          }
        };

        await adapter.CreateOrders(order);
      }
    }

    public double GetPriceChange(double? currentPrice, double? percentChange)
    {
      return (currentPrice * percentChange).Value;
    }
  }
}
