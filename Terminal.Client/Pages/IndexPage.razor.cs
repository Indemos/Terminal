using Canvas.Core.ShapeSpace;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
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

namespace Terminal.Client.Pages
{
  public partial class IndexPage
  {
    [Inject] IConfiguration Configuration { get; set; }

    /// <summary>
    /// Strategy
    /// </summary>
    const string _assetX = "GOOGL";
    const string _assetY = "GOOG";
    const string _account = "Simulation";

    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performer { get; set; }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await View.ChartsView.Create(new GroupShape
        {
          Groups = new Dictionary<string, IGroupShape>
          {
            ["Prices"] = new GroupShape
            {
              Groups = new Dictionary<string, IGroupShape>
              {
                ["Delta"] = new BarShape()
              }
            }
          }
        });

        await View.ReportsView.Create(new GroupShape
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

        View.Setup = () =>
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

          View.Gateway = new Adapter
          {
            Speed = 1,
            Name = _account,
            Source = Configuration["Inputs:Source"]
          };

          Performer = new PerformanceIndicator { Name = "Balance" };
          View.Gateway.Account = account;

          account
            .Instruments
            .Values
            .Select(o => o.PointGroups.ItemStream)
            .Merge()
            .Subscribe(OnData);
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    private void OnData(ITransactionMessage<IPointModel> message)
    {
      var point = message.Next;
      var account = point.Account;
      var instrumentX = point.Account.Instruments[_assetX];
      var instrumentY = point.Account.Instruments[_assetY];
      var seriesX = instrumentX.PointGroups;
      var seriesY = instrumentY.PointGroups;

      if (seriesX.Any() is false || seriesY.Any() is false)
      {
        return;
      }

      var priceX = seriesX.LastOrDefault().Last;
      var priceY = seriesY.LastOrDefault().Last;
      var delta = Math.Abs((priceX - priceY).Value);
      var performance = Performer.Calculate(new[] { account });

      if (account.ActivePositions.Any())
      {
        if (Math.Abs(delta) <= 0.1)
        {
          ClosePositions();
        }
      }

      if (account.ActiveOrders.Any() is false && account.ActivePositions.Any() is false)
      {
        switch (true)
        {
          case true when priceX - priceY >= 5: OpenPositions(instrumentY, instrumentX); break;
          case true when priceY - priceX >= 5: OpenPositions(instrumentX, instrumentY); break;
        }
      }

      var chartPoints = new PointModel[]
      {
        new PointModel { Time = point.Time, Name = "Delta", Last = delta }
      };

      var reportPoints = new[]
      {
        new PointModel { Time = point.Time, Name = Performer.Name, Last = performance.Last }
      };

      View.ChartsView.UpdateItems(chartPoints, 100);
      View.ReportsView.UpdateItems(reportPoints);
      View.DealsView.UpdateItems(account.Positions);
      View.OrdersView.UpdateItems(account.ActiveOrders);
      View.PositionsView.UpdateItems(account.ActivePositions);
    }

    private void OpenPositions(IInstrumentModel assetBuy, IInstrumentModel assetSell)
    {
      View.Gateway.OrderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
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

      View.Gateway.OrderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
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

    private void ClosePositions()
    {
      foreach (var position in View.Gateway.Account.ActivePositions.Values)
      {
        View.Gateway.OrderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
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
