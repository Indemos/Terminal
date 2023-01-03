using System;
using System.Threading.Tasks;
using Terminal.Connector.Simulation;
using Terminal.Core.CollectionSpace;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Tests.Connectors
{
  public class SimulationTests : Adapter, IDisposable
  {
    const string Asset = "SPY";

    public SimulationTests()
    {
      var span = TimeSpan.FromMinutes(1);

      Account = new AccountModel
      {
        Name = "Demo",
        Balance = 50000,
        Instruments = new NameCollection<string, IInstrumentModel>
        {
          [Asset] = new InstrumentModel { Name = Asset, TimeFrame = span }
        }
      };
    }

    [Fact]
    public void CreateMarketOrder()
    {
      var instrument = new InstrumentModel
      {
        Name = Asset
      };

      var point = new PointModel
      {
        Ask = 200,
        Bid = 100,
        AskSize = 1000,
        BidSize = 500,
        Last = 200,
        Instrument = instrument
      };

      var order = new TransactionOrderModel
      {
        Size = 1,
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Instrument = instrument
      };

      instrument.Points.Add(point);

      base.CreateOrders(order);

      //Assert.Null(collection[2]);
      //Assert.Equal(item, collection[0]);
      //Assert.Equal(itemNext, collection[1]);
    }
  }
}
