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
using AreaGroupModel = Canvas.Core.ModelSpace.AreaGroupModel;
using GroupModel = Canvas.Core.ModelSpace.GroupModel;
using IGroupModel = Canvas.Core.ModelSpace.IGroupModel;
using LineGroupModel = Canvas.Core.ModelSpace.LineGroupModel;

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
    protected OrdersComponent OrdersView { get; set; }
    protected PositionsComponent PositionsView { get; set; }

    /// <summary>
    /// Render
    /// </summary>
    /// <param name="setup"></param>
    /// <returns></returns>
    protected override Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        ChartsView.CreateMap(new GroupModel
        {
          Groups = new Dictionary<string, IGroupModel>
          {
            ["Prices"] = new GroupModel
            {
              Groups = new Dictionary<string, IGroupModel>
              {
                ["GOOG"] = new LineGroupModel(),
                ["GOOGL"] = new LineGroupModel()
              }
            },
            ["Performance"] = new GroupModel
            {
              Groups = new Dictionary<string, IGroupModel>
              {
                ["Balance"] = new AreaGroupModel()
              }
            }
          }
        });
      }

      return base.OnAfterRenderAsync(setup);
    }

    protected void OnConnect()
    {
      Setup();

      IsConnection = true;
      IsSubscription = true;

      ChartsView.Clear();

      _adapter.Connect();
    }

    protected void OnDisconnect()
    {
      IsConnection = false;
      IsSubscription = false;

      _adapter.Disconnect();

      ChartsView.Clear();
      ChartsView.Update();
    }

    protected void OnSubscribe()
    {
      IsSubscription = true;

      _adapter.Subscribe();
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
        Account = account,
        Source = "C:/Users/user/Desktop/Code/NET/Terminal/Data/Quotes"
      };

      _performanceIndicator = new PerformanceIndicator { Name = "Balance" };
      _scaleIndicatorX = new ScaleIndicator { Max = 1, Min = -1, Interval = 1, Name = "Indicators : " + _assetX };
      _scaleIndicatorY = new ScaleIndicator { Max = 1, Min = -1, Interval = 1, Name = "Indicators : " + _assetY };

      _adapter
        .Account
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

      if (seriesX.Any() && seriesY.Any())
      {
        if (account.ActiveOrders.Any() is false &&
            account.ActivePositions.Any() is false &&
            Math.Abs(indX.Last.Value - indY.Last.Value) >= 0.5)
        {
          if (indX.Last.Value > indY.Last.Value)
          {
            _adapter.OrderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
            {
              Action = ActionEnum.Create,
              Next = new TransactionOrderModel
              {
                Size = 1,
                Side = OrderSideEnum.Sell,
                Category = OrderCategoryEnum.Market,
                Instrument = instrumentX
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
                Instrument = instrumentX
              }
            });
          }

          if (indX.Last.Value < indY.Last.Value)
          {
            _adapter.OrderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
            {
              Action = ActionEnum.Create,
              Next = new TransactionOrderModel
              {
                Size = 1,
                Side = OrderSideEnum.Buy,
                Category = OrderCategoryEnum.Market,
                Instrument = instrumentX
              }
            });

            _adapter.OrderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
            {
              Action = ActionEnum.Create,
              Next = new TransactionOrderModel
              {
                Size = 1,
                Side = OrderSideEnum.Sell,
                Category = OrderCategoryEnum.Market,
                Instrument = instrumentY
              }
            });
          }
        }

        if (account.ActivePositions.Any() && Math.Abs(indX.Last.Value - indY.Last.Value) < 0.05)
        {
        }
      }

      var points = new[]
      {
        new PointModel { Time = point.Time, Name = _assetX, Last = indX.Last },
        new PointModel { Time = point.Time, Name = _assetY, Last = indY.Last },
        new PointModel { Time = point.Time, Name = _performanceIndicator.Name, Last = performance.Last }
      };

      ChartsView.UpdateItems(points);
      OrdersView.UpdateItems(account.ActiveOrders);
      PositionsView.UpdateItems(account.ActivePositions);
    }
  }
}
