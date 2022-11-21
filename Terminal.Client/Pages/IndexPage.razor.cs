using Canvas.Core.ModelSpace;
using Canvas.Core.ShapeSpace;
using Distribution.DomainSpace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Terminal.Client.Components;
using Terminal.Connector.Simulation;
using Terminal.Core.CollectionSpace;
using Terminal.Core.EnumSpace;
using Terminal.Core.IndicatorSpace;
using Terminal.Core.MessageSpace;
using Terminal.Core.ModelSpace;
using Terminal.Core.ServiceSpace;

namespace Terminal.Client.Pages
{
  public partial class IndexPage
  {
    /// <summary>
    /// Controls
    /// </summary>
    protected bool IsConnection { get; set; }
    protected bool IsSubscription { get; set; }
    protected ChartsComponent ChartsView { get; set; }
    protected ChartsComponent ReportsView { get; set; }
    protected OrdersComponent OrdersView { get; set; }
    protected PositionsComponent PositionsView { get; set; }
    protected StatementsComponent StatementsView { get; set; }

    protected async Task OnConnect()
    {
      OnDisconnect();
      Setup();

      IsConnection = true;
      IsSubscription = true;

      await _adapter.Connect();
    }

    protected void OnDisconnect()
    {
      IsConnection = false;
      IsSubscription = false;

      _adapter?.Disconnect();

      ChartsView.Clear();
      ReportsView.Clear();
    }

    protected async Task OnSubscribe()
    {
      IsSubscription = true;

      await _adapter.Subscribe();
    }

    protected void OnUnsubscribe()
    {
      IsSubscription = false;

      _adapter.Unsubscribe();
    }

    protected void OnOpenDeals()
    {
    }

    protected void OnOpenStatements()
    {
      InstanceService<Scene>.Instance.Scheduler.Send(() =>
      {
        if (_adapter?.Account is not null)
        {
          StatementsView.UpdateItems(new[] { _adapter.Account });
        }
      });
    }

    /// <summary>
    /// Render
    /// </summary>
    /// <param name="setup"></param>
    /// <returns></returns>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await ChartsView.Create(new GroupShape
        {
          Groups = new Dictionary<string, IGroupShape>
          {
            ["Prices"] = new GroupShape
            {
              Groups = new Dictionary<string, IGroupShape>
              {
                ["GOOG"] = new LineShape(),
                ["GOOGL"] = new LineShape()
              }
            }
          }
        });

        await ReportsView.Create(new GroupShape
        {
          Groups = new Dictionary<string, IGroupShape>
          {
            ["Performance"] = new GroupShape
            {
              Groups = new Dictionary<string, IGroupShape>
              {
                ["Balance"] = new AreaShape()
              }
            }
          }
        });
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// Strategy
    /// </summary>
    const string _assetX = "GOOG";
    const string _assetY = "GOOGL";
    const string _account = "Simulation";

    Adapter _adapter = null;
    ScaleIndicator _scaleIndicatorX = null;
    ScaleIndicator _scaleIndicatorY = null;
    PerformanceIndicator _performanceIndicator = null;

    protected void Setup()
    {
      var span = TimeSpan.FromMinutes(1);
      var account = new AccountModel
      {
        Balance = 50000,
        Name = _account,
        Instruments = new NameCollection<string, IInstrumentModel>
        {
          [_assetX] = new InstrumentModel { Name = _assetX, TimeFrame = span },
          [_assetY] = new InstrumentModel { Name = _assetY, TimeFrame = span }
        }
      };

      _adapter = new Adapter
      {
        Speed = 1,
        Name = _account,
        Source = "C:/Users/user/Desktop/Code/NET/Terminal/Data/Quotes"
      };

      _performanceIndicator = new PerformanceIndicator { Name = "Balance" };
      _scaleIndicatorX = new ScaleIndicator { Max = 1, Min = -1, Interval = 1, Name = "Indicators : " + _assetX };
      _scaleIndicatorY = new ScaleIndicator { Max = 1, Min = -1, Interval = 1, Name = "Indicators : " + _assetY };
      _adapter.Account = account;

      account
        .Instruments
        .Values
        .Select(o => o.PointGroups.ItemStream)
        .Merge()
        .Subscribe(OnData);
    }

    protected void OnData(ITransactionMessage<IPointModel> message)
    {
      var point = message.Next;
      var account = point.Account;
      var instrumentX = point.Account.Instruments[_assetX];
      var instrumentY = point.Account.Instruments[_assetY];
      var seriesX = instrumentX.PointGroups;
      var seriesY = instrumentY.PointGroups;
      var indX = _scaleIndicatorX.Calculate(seriesX);
      var indY = _scaleIndicatorY.Calculate(seriesY);
      var performance = _performanceIndicator.Calculate(new[] { account });

      if (seriesX.Any() is false || seriesY.Any() is false)
      {
        return;
      }

      if (account.ActivePositions.Any())
      {
        if (Math.Abs(indX.Last.Value - indY.Last.Value) <= 0.1)
        {
          ClosePositions();
        }
      }

      if (account.ActiveOrders.Any() is false && account.ActivePositions.Any() is false)
      {
        switch (true)
        {
          case true when indX.Last.Value - indY.Last.Value >= 0.5: OpenPositions(instrumentY, instrumentX); break;
          case true when indY.Last.Value - indX.Last.Value >= 0.5: OpenPositions(instrumentX, instrumentY); break;
        }
      }

      var chartPoints = new[]
      {
        new PointModel { Time = point.Time, Name = _assetX, Last = indX.Last },
        new PointModel { Time = point.Time, Name = _assetY, Last = indY.Last }
      };

      var reportPoints = new[]
      {
        new PointModel { Time = point.Time, Name = _performanceIndicator.Name, Last = performance.Last }
      };

      ChartsView.UpdateItems(chartPoints, 100);
      ReportsView.UpdateItems(reportPoints);
      OrdersView.UpdateItems(account.ActiveOrders);
      PositionsView.UpdateItems(account.ActivePositions);
    }

    protected void OpenPositions(IInstrumentModel assetBuy, IInstrumentModel assetSell)
    {
      _adapter.OrderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
      {
        Action = ActionEnum.Create,
        Next = new TransactionOrderModel
        {
          Size = 1,
          Side = OrderSideEnum.Sell,
          Category = OrderCategoryEnum.Market,
          Instrument = assetSell
        }
      });

      _adapter.OrderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
      {
        Action = ActionEnum.Create,
        Next = new TransactionOrderModel
        {
          Size = 1,
          Side = OrderSideEnum.Buy,
          Category = OrderCategoryEnum.Market,
          Instrument = assetBuy
        }
      });
    }

    protected void ClosePositions()
    {
      foreach (var position in _adapter.Account.ActivePositions.Values)
      {
        _adapter.OrderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
        {
          Action = ActionEnum.Create,
          Next = new TransactionOrderModel
          {
            Size = 1,
            Side = Equals(position.Side, OrderSideEnum.Buy) ? OrderSideEnum.Sell : OrderSideEnum.Buy,
            Category = OrderCategoryEnum.Market,
            Instrument = position.Instrument
          }
        });
      }
    }

  }
}
