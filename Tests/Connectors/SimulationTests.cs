using System;
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
    public void ValidateMissingInstrument()
    {
      var order = new TransactionOrderModel();

      Assert.Throws<ArgumentNullException>(() => base.ValidateOrders(order));
    }

    [Fact]
    public void ValidateIncorrectInstrument()
    {
      var instrument = new InstrumentModel();
      var order = new TransactionOrderModel
      {
        Size = 1,
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Instrument = instrument
      };

      var errors = base.ValidateOrders(order);

      Assert.Equal(nameof(instrument.Name), errors[0].PropertyName);
      Assert.Equal(nameof(instrument.TimeFrame), errors[1].PropertyName);
      Assert.Equal(nameof(instrument.Points), errors[2].PropertyName);
      Assert.Equal(nameof(instrument.PointGroups), errors[3].PropertyName);

      var errorCode = "NotEmptyValidator";

      Assert.Equal(errorCode, errors[0].ErrorCode);
      Assert.Equal(errorCode, errors[1].ErrorCode);
      Assert.Equal(errorCode, errors[2].ErrorCode);
      Assert.Equal(errorCode, errors[3].ErrorCode);
    }

    [Fact]
    public void ValidateIncorrectOrder()
    {
      var instrument = new InstrumentModel();
      var order = new TransactionOrderModel
      {
        Instrument = instrument
      };

      var errors = base.ValidateOrders(order);

      Assert.Equal(nameof(order.Side), errors[0].PropertyName);
      Assert.Equal(nameof(order.Size), errors[1].PropertyName);
      Assert.Equal(nameof(order.Type), errors[2].PropertyName);
      Assert.Equal(nameof(order.Price), errors[3].PropertyName);

      var errorCode = "NotEmptyValidator";

      Assert.Equal(errorCode, errors[0].ErrorCode);
      Assert.Equal(errorCode, errors[1].ErrorCode);
      Assert.Equal(errorCode, errors[2].ErrorCode);
      Assert.Equal(errorCode, errors[3].ErrorCode);
    }

    [Fact]
    public void ValidateIncorrectQuote()
    {
      var instrument = new InstrumentModel
      {
        Name = Asset,
        TimeFrame = TimeSpan.FromSeconds(1)
      };

      var point = new PointModel
      {
        //Ask = 200,
        //Bid = 100,
        //AskSize = 1000,
        //BidSize = 500,
        //Last = 200,
        //Instrument = instrument
      };

      var order = new TransactionOrderModel
      {
        Size = 1,
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Instrument = instrument
      };

      instrument.Points.Add(point);
      instrument.PointGroups.Add(point);

      var errors = base.ValidateOrders(order);
    }
  }
}
