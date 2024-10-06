using Canvas.Core.Models;
using Canvas.Core.Shapes;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Components;
using Terminal.Core.Domains;
using Terminal.Core.Indicators;
using Terminal.Core.Models;

namespace Terminal.Pages.Options
{
  public partial class CreditSpreadReversal
  {
    public virtual OptionPageComponent OptionView { get; set; }
    public virtual RsiIndicator Rsi { get; set; }
    public virtual double Price { get; set; }

    /// <summary>
    /// Setup views and adapters
    /// </summary>
    /// <param name="setup"></param>
    /// <returns></returns>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        OptionView.Instrument = new InstrumentModel
        {
          Name = "SPY",
          TimeFrame = TimeSpan.FromMinutes(5)
        };

        Rsi = new RsiIndicator
        {
          Interval = 5,
          Name = nameof(Rsi)
        };

        var groups = Enumerable.Range(0, 1).Select(o => new Shape()).ToList();

        groups[0].Groups["Rsi"] = new LineShape { Component = new ComponentModel { Color = SKColors.LimeGreen } };

        await OptionView.OnLoad(OnData, groups);
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// Process tick data
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected async Task OnData(PointModel point)
    {
      await OptionView.OnUpdate(point, async options =>
      {
        var adapter = OptionView.View.Adapters["Sim"];
        var account = adapter.Account;
        var rsi = Rsi.Calculate(account.Instruments.Values.First().PointGroups);
        var chartPoints = new List<KeyValuePair<string, PointModel>>();
        var posSide = account
          .Positions
          .FirstOrDefault()
          .Value
          ?.Transaction
          ?.Instrument
          ?.Derivative
          ?.Side;

        if (rsi.Values.Count > rsi.Interval)
        {
          if (rsi.Point.Last < 30 && posSide is not Core.Enums.OptionSideEnum.Put)
          {
            var orders = OptionView.GetCreditSpread(Core.Enums.OptionSideEnum.Put, point, options);

            if (orders.Count > 0)
            {
              Price = point.Last.Value;
              await OptionView.ClosePositions();
              await adapter.CreateOrders([.. orders]);
            }
          }

          if (rsi.Point.Last > 70 && posSide is not Core.Enums.OptionSideEnum.Call)
          {
            var orders = OptionView.GetCreditSpread(Core.Enums.OptionSideEnum.Call, point, options);

            if (orders.Count > 0)
            {
              Price = -point.Last.Value;
              await OptionView.ClosePositions();
              await adapter.CreateOrders([.. orders]);
            }
          }
        }

        //var shareOrders = await OptionView.GetDirectionHedge(Price, point);

        //if (shareOrders.Count > 0)
        //{
        //  var orderResponse = await adapter.CreateOrders([.. shareOrders]);
        //}

        chartPoints.Add(KeyValuePair.Create("Rsi", new PointModel { Time = point.Time, Last = rsi.Point.Last }));

        OptionView.View.ChartsView.UpdateItems(chartPoints);
      });
    }
  }
}
