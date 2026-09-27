using Canvas.Core.Shapes;
using Core.Enums;
using Core.Groups;
using Core.Indicators;
using Core.Models;
using Core.Services;
using Dashboard.Components;
using Microsoft.AspNetCore.Components;
using Simulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Futures
{
  public partial class Demo
  {
    [Inject] StateService Subscription { get; set; }

    ChartsComponent DataView { get; set; }
    ChartsComponent ScoreView { get; set; }
    ChartsComponent IndicatorsView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    TimeGroup SpanDay { get; set; }
    TimeGroup SpanMin { get; set; }
    TimeGroup SpanHour { get; set; }
    int PosDirection { get; set; }
    int Direction { get; set; }

    const string symName = "ES";
    Dictionary<string, Instrument> Instruments = new()
    {
      [symName] = new() { Name = symName, Leverage = 50, Commission = 3.65, StepSize = 0.25 },
    };

    protected override async Task OnView()
    {
      await DataView.Create(nameof(DataView));
      await ScoreView.Create(nameof(ScoreView));
      await IndicatorsView.Create(nameof(IndicatorsView));
      await PerformanceView.Create(nameof(PerformanceView));

      DataView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      ScoreView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      IndicatorsView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
    }

    protected override Task OnTrade()
    {
      var adapter = Adapter = new SimGateway
      {
        Connector = Connector,
        Source = Configuration["Documents:Resources"] + "/FUTS",
        Account = new()
        {
          Descriptor = "Demo",
          Balance = 25000,
          Instruments = Instruments
        }
      };

      Performance = new();
      SpanDay = new() { TimeFrame = TimeSpan.FromDays(1) };
      SpanMin = new() { TimeFrame = TimeSpan.FromMinutes(1) };
      SpanHour = new() { TimeFrame = TimeSpan.FromHours(1) };

      return base.OnTrade();
    }

    protected async void Render(Instrument instrument)
    {
      var adapter = Adapter;
      var account = adapter.Account;
      var price = instrument.Price;
      var index = price.Time.Value;
      var performance = await Performance.Update([adapter]);

      OrdersView.Update(Adapters.Values);
      PositionsView.Update(Adapters.Values);
      TransactionsView.Update(Adapters.Values);
      DataView.Update(index, nameof(DataView), "Price", new LineShape { Y = price.Last, Component = Com });
      PerformanceView.Update(index, nameof(PerformanceView), "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(index, nameof(PerformanceView), "PnL", new LineShape { Y = performance, Component = ComDown });
      //IndicatorsView.Update(spanRenko.Bar.Time.Value, nameof(IndicatorsView), "Indicators", IndicatorsView.GetShape<CandleShape>(spanRenko));
    }

    protected override async Task OnTradeUpdate(Instrument instrument)
    {
      var price = instrument.Price;
      var index = price.Time.Value;
      var adapter = Adapter;
      var account = adapter.Account;
      var asset = account.Instruments[symName];
      var orders = (await adapter.GetOrders(default)).Data;
      var positions = (await adapter.GetPositions(default)).Data;

      var spanDay = SpanDay.Update(price);
      var spanMin = SpanMin.Update(price);
      var spanHour = SpanHour.Update(price);

      var isMinLong = spanMin.Bar.Close > spanMin.Bar.Open;
      var isMinShort = spanMin.Bar.Close < spanMin.Bar.Open;
      var isDayLong = spanDay.Bar.Close > spanDay.Bar.Open;
      var isDayShort = spanDay.Bar.Close < spanDay.Bar.Open;
      var isHourLong = spanHour.Bar.Close > spanHour.Bar.Open;
      var isHourShort = spanHour.Bar.Close < spanHour.Bar.Open;
      var isLong = isMinLong && isHourLong && isDayLong;
      var isShort = isMinShort && isHourShort && isDayShort;

      if (positions.Count is 0)
      {
        switch (true)
        {
          case true when isLong:
            PosDirection = 1;
            await OpenPosition(adapter, Instruments[symName], OrderSideEnum.Long);
            break;

          case true when isShort:
            PosDirection = -1;
            await OpenPosition(adapter, Instruments[symName], OrderSideEnum.Short);
            break;
        }
      }

      if (positions.Count is not 0)
      {
        var closeLong = PosDirection > 0 && isHourShort;
        var closeShort = PosDirection < 0 && isHourLong;

        if (closeLong || closeShort)
        {
          await ClosePosition(adapter);
        }
      }

      Render(instrument);
    }
  }
}
