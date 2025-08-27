using Simulation;
using System;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class ClearOrders : Adapter, IDisposable
  {
    public ClearOrders()
    {
      Account = new Account
      {
        Descriptor = "Demo",
        Balance = 50000
      };
    }

    private void SetStates(params InstrumentModel[] instruments)
    {
      foreach (var o in instruments)
      {
        Account.States.Get(o.Name).Instrument = new InstrumentModel
        {
          Name = o.Name,
          Point = o.Point,
        };
      }
    }

    [Fact]
    public async Task CancelOrders()
    {
      var assetX = new InstrumentModel
      {
        Name = "SPY",
        Point = new PointModel { Bid = 545, Ask = 550, Last = 550, Time = DateTime.Now, }
      };

      var assetY = new InstrumentModel
      {
        Name = "MSFT",
        Point = new PointModel { Bid = 545, Ask = 550, Last = 550, Time = DateTime.Now, }
      };

      var orderX = new OrderModel
      {
        Amount = 5,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Stop,
        Price = assetX.Point.Ask + 5,
        Transaction = new TransactionModel
        {
          Instrument = assetX
        }
      };

      var orderY = new OrderModel
      {
        Amount = 5,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Stop,
        Price = assetY.Point.Ask + 5,
        Transaction = new TransactionModel
        {
          Instrument = assetY
        }
      };

      SetStates(assetX, assetY);

      await base.SendOrder(orderX);
      await base.SendOrder(orderY);

      Assert.Empty(Account.Deals);
      Assert.Empty(Account.Positions);
      Assert.Equal(2, Account.Orders.Count);

      await ClearOrders(orderX);

      Assert.Empty(Account.Deals);
      Assert.Empty(Account.Positions);
      Assert.Single(Account.Orders);
      Assert.Equal(Account.Orders.First().Value.Name, assetY.Name);
    }
  }
}
