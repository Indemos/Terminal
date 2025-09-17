//using Core.Common.Enums;
//using Core.Common.Grains;
//using Core.Common.States;
//using Core.Common.Tests;
//using Moq;
//using Orleans;
//using Orleans.Streams;
//using Orleans.TestingHost;
//using Simulation.Grains;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text.Json;
//using System.Threading.Tasks;

//namespace Simulation.Endpoints.Tests
//{
//  public class AdapterTests : IDisposable
//  {
//    private readonly Mock<IConnectionGrain> _mockConnectionGrain;
//    private readonly Mock<IClusterClient> _mockConnector;
//    private readonly Mock<Adapter> _mockAdapter;
//    private readonly TestCluster _cluster;
//    private readonly Adapter _adapter;

//    private string Descriptor => JsonSerializer.Serialize(new Descriptor
//    {
//      Account = "Demo"
//    });

//    private AccountState CreateTestAccount()
//    {
//      return new AccountState
//      {
//        Descriptor = "Demo",
//        Instruments = new Dictionary<string, InstrumentState>
//        {
//          ["AAPL"] = new InstrumentState { Name = "AAPL" },
//          ["MSFT"] = new InstrumentState { Name = "MSFT" }
//        }
//      };
//    }

//    public AdapterTests()
//    {
//      _mockConnector = new Mock<IClusterClient>();
//      _mockAdapter = new Mock<Adapter> { CallBase = true };
//      _mockConnectionGrain = new Mock<IConnectionGrain> { CallBase = true };

//      _mockConnectionGrain
//        .Setup(x => x.Connect(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Dictionary<string, InstrumentState>>()))
//        .ReturnsAsync(new StatusResponse { Data = StatusEnum.Active });

//      _mockConnectionGrain
//        .Setup(x => x.Disconnect())
//        .ReturnsAsync(new StatusResponse { Data = StatusEnum.Inactive });

//      _mockConnector
//        .Setup(x => x.GetGrain<IConnectionGrain>(It.IsAny<string>(), It.IsAny<string>()))
//        .Returns(_mockConnectionGrain.Object);

//      var mockStream = new Mock<IAsyncStream<PriceState>>();
//      var mockOrderStream = new Mock<IAsyncStream<OrderState>>();

//      _mockAdapter.Setup(o => o.Speed).Returns(500);
//      _mockAdapter.Setup(o => o.Source).Returns("demo-source");
//      _mockAdapter.Setup(o => o.account).Returns(CreateTestAccount());
//      _mockAdapter.Setup(o => o.Connector).Returns(_mockConnector.Object);

//      _adapter = _mockAdapter.Object;

//      var builder = new TestClusterBuilder();

//      builder.AddSiloBuilderConfigurator<SiloConfigurator>();
//      builder.AddClientBuilderConfigurator<SiloConfigurator>();

//      _cluster = builder.Build();
//      _cluster.Deploy();
//    }

//    public void Dispose()
//    {
//      _adapter?.Dispose();
//      _cluster.StopAllSilos();
//    }

//    [Fact]
//    public async Task Connect_ShouldReturnActiveStatus()
//    {
//      // Act
//      var result = await _adapter.Connect();

//      // Assert
//      Assert.NotNull(result);
//      Assert.Equal(StatusEnum.Active, result.Data);

//      _mockConnectionGrain.Verify(o => o.Connect("demo-source", 500, new()), Times.Once);
//    }

//    [Fact]
//    public async Task Disconnect_ShouldReturnInactiveStatus()
//    {
//      // Act
//      var result = await _adapter.Disconnect();

//      // Assert
//      Assert.NotNull(result);
//      Assert.Equal(StatusEnum.Inactive, result.Data);

//      _mockConnectionGrain.Verify(o => o.Disconnect(), Times.Once);
//    }

//    [Fact]
//    public async Task Subscribe_ShouldReturnActiveStatus()
//    {
//      // Arrange
//      var instrument = new InstrumentState { Name = "AAPL" };
//      var mockConnectionGrain = new Mock<IConnectionGrain> { CallBase = true };

//      mockConnectionGrain
//        .Setup(x => x.Subscribe(It.IsAny<InstrumentState>()))
//        .Returns(Task.FromResult(new StatusResponse()));

//      _mockConnector
//        .Setup(x => x.GetGrain<IConnectionGrain>(It.IsAny<string>(), It.IsAny<string>()))
//        .Returns(mockConnectionGrain.Object);

//      // Act
//      var result = await _adapter.Subscribe(instrument);

//      // Assert
//      Assert.NotNull(result);
//      Assert.Equal(StatusEnum.Active, result.Data);

//      mockConnectionGrain.Verify(o => o.Subscribe(instrument), Times.Once);
//    }

//    [Fact]
//    public async Task Unsubscribe_ShouldReturnPauseStatus()
//    {
//      // Arrange
//      var instrument = new InstrumentState { Name = "AAPL" };
//      var mockConnectionGrain = new Mock<IConnectionGrain> { CallBase = true };

//      mockConnectionGrain
//        .Setup(x => x.Unsubscribe(It.IsAny<InstrumentState>()))
//        .Returns(Task.FromResult(new StatusResponse()));

//      _mockConnector
//        .Setup(x => x.GetGrain<IConnectionGrain>(It.IsAny<string>(), It.IsAny<string>()))
//        .Returns(mockConnectionGrain.Object);

//      // Act
//      var result = await _adapter.Unsubscribe(instrument);

//      // Assert
//      Assert.NotNull(result);
//      Assert.Equal(StatusEnum.Pause, result.Data);

//      mockConnectionGrain.Verify(o => o.Unsubscribe(instrument), Times.Once);
//    }

//    [Fact]
//    public async Task GetDom_ShouldReturnDomResponse()
//    {
//      // Arrange
//      var criteria = new MetaState { Instrument = new InstrumentState { Name = "AAPL" } };
//      var bids = Enumerable.Range(1, 5)
//        .Select(i => new PriceState { Time = DateTime.Now.AddMinutes(i).Ticks, Last = 100 + i })
//        .ToList();
//      var asks = Enumerable.Range(1, 5)
//        .Select(i => new PriceState { Time = DateTime.Now.AddMinutes(i).Ticks, Last = 100 + i })
//        .ToList();

//      var dom = new DomState
//      {
//        Bids = bids,
//        Asks = asks
//      };

//      var grain = _cluster
//        .GrainFactory
//        .GetGrain<IDomGrain>(Descriptor);

//      _mockConnector
//        .Setup(x => x.GetGrain<IDomGrain>(It.IsAny<string>(), It.IsAny<string>()))
//        .Returns(grain);

//      await grain.Store(dom);

//      // Act
//      var result = await _adapter.Dom(criteria);

//      // Assert
//      Assert.NotNull(result);
//      Assert.Equal(JsonSerializer.Serialize(dom), JsonSerializer.Serialize(result.Data));
//    }

//    //[Fact]
//    //public async Task GetBars_WithDateFilters_ShouldReturnFilteredPrices()
//    //{
//    //  // Arrange
//    //  var minDate = new DateTime(2023, 1, 1).Ticks;
//    //  var maxDate = new DateTime(2023, 12, 31).Ticks;
//    //  var criteria = new MetaState
//    //  {
//    //    Instrument = new InstrumentState { Name = "AAPL" },
//    //    MinDate = minDate,
//    //    MaxDate = maxDate,
//    //    Count = 10
//    //  };

//    //  var priceData = new List<PriceState>
//    //  {
//    //    new() { Time = new DateTime(2022, 12, 31).Ticks, Last = 100, Bar = new() {} }, // Before range
//    //    new() { Time = new DateTime(2023, 6, 15).Ticks, Last = 150, Bar = new() {} },  // In range
//    //    new() { Time = new DateTime(2023, 8, 15).Ticks, Last = 160, Bar = new() {} },  // In range
//    //    new() { Time = new DateTime(2024, 1, 1).Ticks, Last = 200, Bar = new() {} }    // After range
//    //  };

//    //  var grain = _cluster
//    //    .GrainFactory
//    //    .GetGrain<IPricesGrainAdapter>(Descriptor);

//    //  foreach (var price in priceData)
//    //  {
//    //    await grain.Send(price);
//    //  }

//    //  _mockConnector
//    //    .Setup(x => x.GetGrain<IPricesGrainAdapter>(It.IsAny<string>(), It.IsAny<string>()))
//    //    .Returns(grain);

//    //  // Act
//    //  var result = await _adapter.GetBars(criteria);

//    //  // Assert
//    //  Assert.NotNull(result);
//    //  Assert.Equal(2, result.Data.Count);
//    //  Assert.All(result.Data, price => Assert.True(price.Time >= minDate && price.Time <= maxDate));
//    //}

//    //[Fact]
//    //public async Task GetTicks_WithCountLimit_ShouldReturnLimitedData()
//    //{
//    //  // Arrange
//    //  var criteria = new MetaState
//    //  {
//    //    Instrument = new InstrumentState { Name = "AAPL" },
//    //    Count = 2
//    //  };

//    //  var priceData = Enumerable.Range(1, 5)
//    //    .Select(i => new PriceState { Time = DateTime.Now.AddMinutes(i).Ticks, Last = 100 + i })
//    //    .ToList();

//    //  var grain = _cluster
//    //    .GrainFactory
//    //    .GetGrain<IPricesGrainAdapter>(Descriptor);

//    //  foreach (var price in priceData)
//    //  {
//    //    await grain.Send(price);
//    //  }

//    //  _mockConnector
//    //    .Setup(x => x.GetGrain<IPricesGrainAdapter>(It.IsAny<string>(), It.IsAny<string>()))
//    //    .Returns(grain);

//    //  // Act
//    //  var result = await _adapter.GetTicks(criteria);

//    //  // Assert
//    //  Assert.NotNull(result);
//    //  Assert.Equal(2, result.Data.Count);
//    //  Assert.Equal(priceData.TakeLast(2), result.Data);
//    //}

//    [Fact]
//    public async Task GetOptions_WithStrikeFilters_ShouldReturnFilteredOptions()
//    {
//      // Arrange
//      var criteria = new MetaState
//      {
//        Instrument = new InstrumentState { Name = "AAPL" },
//        MinPrice = 140,
//        MaxPrice = 160
//      };

//      var optionsData = new List<InstrumentState>
//      {
//        new() { Name = "AAPL_Call_130", Derivative = new DerivativeState { Strike = 130, Side = OptionSideEnum.Call } },
//        new() { Name = "AAPL_Call_150", Derivative = new DerivativeState { Strike = 150, Side = OptionSideEnum.Call } },
//        new() { Name = "AAPL_Call_170", Derivative = new DerivativeState { Strike = 170, Side = OptionSideEnum.Call } }
//      };

//      var grain = _cluster
//        .GrainFactory
//        .GetGrain<IOptionsGrainAdapter>(Descriptor);

//      _mockConnector
//        .Setup(x => x.GetGrain<IOptionsGrainAdapter>(It.IsAny<string>(), It.IsAny<string>()))
//        .Returns(grain);

//      await grain.Store(optionsData);

//      // Act
//      var result = await _adapter.GetOptions(criteria);

//      // Assert
//      Assert.NotNull(result);
//      Assert.Single(result.Data);
//      Assert.Equal(150, result.Data.First().Derivative.Strike);
//    }

//    [Fact]
//    public async Task GetOptions_WithExpirationDateFilter_ShouldFilterCorrectly()
//    {
//      // Arrange
//      var expDate1 = new DateTime(2024, 1, 15);
//      var expDate2 = new DateTime(2024, 2, 15);
//      var expDate3 = new DateTime(2024, 3, 15);

//      var criteria = new MetaState
//      {
//        Instrument = new InstrumentState { Name = "AAPL" },
//        MinDate = new DateTime(2024, 1, 1).Ticks,
//        MaxDate = new DateTime(2024, 2, 28).Ticks
//      };

//      var optionsData = new List<InstrumentState>
//      {
//        new() { Name = "AAPL_Call_1", Derivative = new DerivativeState { ExpirationDate = expDate1, Strike = 150 } },
//        new() { Name = "AAPL_Call_2", Derivative = new DerivativeState { ExpirationDate = expDate2, Strike = 150 } },
//        new() { Name = "AAPL_Call_3", Derivative = new DerivativeState { ExpirationDate = expDate3, Strike = 150 } }
//      };

//      var grain = _cluster
//        .GrainFactory
//        .GetGrain<IOptionsGrainAdapter>(Descriptor);

//      _mockConnector
//        .Setup(x => x.GetGrain<IOptionsGrainAdapter>(It.IsAny<string>(), It.IsAny<string>()))
//        .Returns(grain);

//      await grain.Store(optionsData);

//      // Act
//      var result = await _adapter.GetOptions(criteria);

//      // Assert
//      Assert.NotNull(result);
//      Assert.Equal(2, result.Data.Count);
//      Assert.All(result.Data, o => Assert.True(
//        o.Derivative.ExpirationDate?.Date >= new DateTime(criteria.MinDate.Value) &&
//        o.Derivative.ExpirationDate?.Date <= new DateTime(criteria.MaxDate.Value)));
//    }

//    [Fact]
//    public async Task GetAccount_ShouldReturnAccountResponse()
//    {
//      // Act
//      var result = await _adapter.GetAccount();

//      // Assert
//      Assert.NotNull(result);
//      Assert.Equal(_adapter.account, result.Data);
//    }

//    [Fact]
//    public async Task GetOrders_ShouldReturnOrdersResponse()
//    {
//      // Arrange
//      var expectation = new OrderState[]
//      {
//        new() { Id = "order1", Operation = new() { Status = OrderStatusEnum.Order } },
//        new() { Id = "order2", Operation = new() { Status = OrderStatusEnum.Order } }
//      };

//      var grain = _cluster
//        .GrainFactory
//        .GetGrain<IOrdersGrain>(Descriptor);

//      _mockConnector
//        .Setup(x => x.GetGrain<IOrdersGrain>(It.IsAny<string>(), It.IsAny<string>()))
//        .Returns(grain);

//      foreach (var order in expectation)
//      {
//        await grain.Send(order);
//      }

//      // Act
//      var result = await _adapter.GetOrders(default);

//      // Assert
//      Assert.NotNull(result);
//      Assert.Equal(JsonSerializer.Serialize(expectation), JsonSerializer.Serialize(result.Data));
//    }

//    [Fact]
//    public async Task GetPositions_ShouldReturnPositionsResponse()
//    {
//      // Arrange
//      var instrument = new InstrumentState { Name = "AAPL" };
//      var expectation = new OrderState[]
//      {
//        new()
//        {
//          Id = "order1",
//          Amount = 1,
//          Side = OrderSideEnum.Long,
//          Operation = new()
//          {
//            Status = OrderStatusEnum.Position,
//            Instrument = instrument,
//            Amount = 1
//          }
//        }
//      };

//      var grain = _cluster
//        .GrainFactory
//        .GetGrain<IPositionsGrain>(Descriptor);

//      _mockConnector
//        .Setup(x => x.GetGrain<IPositionsGrain>(It.IsAny<string>(), It.IsAny<string>()))
//        .Returns(grain);

//      foreach (var order in expectation)
//      {
//        await grain.Send(order);
//      }

//      // Act
//      var result = await _adapter.GetPositions(default);

//      // Assert
//      Assert.NotNull(result);
//      Assert.Equal(JsonSerializer.Serialize(expectation), JsonSerializer.Serialize(result.Data));
//    }

//    [Fact]
//    public async Task GetTransactions_ShouldReturnTransactionsResponse()
//    {
//      // Arrange
//      var expectation = new OrderState[]
//      {
//        new() { Id = "order1", Operation = new() { Status = OrderStatusEnum.Transaction } },
//        new() { Id = "order2", Operation = new() { Status = OrderStatusEnum.Transaction } }
//      };

//      var grain = _cluster
//        .GrainFactory
//        .GetGrain<ITransactionsGrain>(Descriptor);

//      _mockConnector
//        .Setup(x => x.GetGrain<ITransactionsGrain>(It.IsAny<string>(), It.IsAny<string>()))
//        .Returns(grain);

//      foreach (var order in expectation)
//      {
//        await grain.Send(order);
//      }

//      // Act
//      var result = await _adapter.GetTransactions(default);

//      // Assert
//      Assert.NotNull(result);
//      Assert.Equal(JsonSerializer.Serialize(expectation), JsonSerializer.Serialize(result.Data));
//    }

//    [Fact]
//    public async Task ClearOrder_ShouldReturnDescriptorResponse()
//    {
//      // Arrange
//      var instrument = new InstrumentState { Name = "AAPL" };
//      var expectation = new OrderState[]
//      {
//        new()
//        {
//          Id = "order1",
//          Amount = 1,
//          Side = OrderSideEnum.Long,
//          Operation = new() { Instrument = instrument }
//        },
//        new()
//        {
//          Id = "order2",
//          Amount = 1,
//          Side = OrderSideEnum.Long,
//          Operation = new() { Instrument = instrument }
//        }
//      };

//      var grain = _cluster
//        .GrainFactory
//        .GetGrain<IOrdersGrain>(Descriptor);

//      _mockConnector
//        .Setup(x => x.GetGrain<IOrdersGrain>(It.IsAny<string>(), It.IsAny<string>()))
//        .Returns(grain);

//      foreach (var order in expectation)
//      {
//        await _adapter.SendOrder(order);
//      }

//      // Act
//      await _adapter.ClearOrder(new() { Id = "order1" });
//      var result = await grain.Orders(new() { Instrument = instrument });

//      // Assert
//      Assert.NotNull(result);
//      Assert.Equal("order2", result.First().Id);
//    }
//  }
//}
