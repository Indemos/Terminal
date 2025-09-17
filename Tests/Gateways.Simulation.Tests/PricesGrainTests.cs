using Core.Common.States;
using Core.Common.Tests;
using Moq;
using Orleans;
using Orleans.TestingHost;
using Simulation.Grains;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Simulation.Prices.Tests
{
  public class PricesGrainTests : IDisposable
  {
    private readonly Mock<IClusterClient> _mockConnector;
    private readonly TestCluster _cluster;

    private string Descriptor => JsonSerializer.Serialize(new DescriptorState
    {
      Account = "Demo"
    });

    public PricesGrainTests()
    {
      _mockConnector = new Mock<IClusterClient>();

      var builder = new TestClusterBuilder();

      builder.AddSiloBuilderConfigurator<SiloConfigurator>();
      builder.AddClientBuilderConfigurator<SiloConfigurator>();

      _cluster = builder.Build();
      _cluster.Deploy();
    }

    public void Dispose()
    {
      _cluster.StopAllSilos();
    }

    //[Fact]
    //public async Task GetBars_WithDateFilters_ShouldReturnFilteredPrices()
    //{
    //  // Arrange
    //  var minDate = new DateTime(2023, 1, 1).Ticks;
    //  var maxDate = new DateTime(2023, 12, 31).Ticks;
    //  var criteria = new MetaState
    //  {
    //    Instrument = new InstrumentState { Name = "AAPL" },
    //    MinDate = minDate,
    //    MaxDate = maxDate,
    //    Count = 10
    //  };

    //  var priceData = new List<PriceState>
    //  {
    //    new() { Time = new DateTime(2022, 12, 31).Ticks, Last = 100, Bar = new() {} }, // Before range
    //    new() { Time = new DateTime(2023, 6, 15).Ticks, Last = 150, Bar = new() {} },  // In range
    //    new() { Time = new DateTime(2023, 8, 15).Ticks, Last = 160, Bar = new() {} },  // In range
    //    new() { Time = new DateTime(2024, 1, 1).Ticks, Last = 200, Bar = new() {} }    // After range
    //  };

    //  var grain = _cluster
    //    .GrainFactory
    //    .GetGrain<IPricesGrainAdapter>(Descriptor);

    //  foreach (var price in priceData)
    //  {
    //    await grain.Send(price);
    //  }

    //  _mockConnector
    //    .Setup(x => x.GetGrain<IPricesGrainAdapter>(It.IsAny<string>(), It.IsAny<string>()))
    //    .Returns(grain);

    //  // Act
    //  var result = await grain.PriceGroups(criteria);

    //  // Assert
    //  Assert.NotNull(result);
    //  Assert.Equal(2, result.Data.Count);
    //  Assert.All(result.Data, price => Assert.True(price.Time >= minDate && price.Time <= maxDate));
    //}

    //[Fact]
    //public async Task GetTicks_WithCountLimit_ShouldReturnLimitedData()
    //{
    //  // Arrange
    //  var criteria = new MetaState
    //  {
    //    Instrument = new InstrumentState { Name = "AAPL" },
    //    Count = 2
    //  };

    //  var priceData = Enumerable.Range(1, 5)
    //    .Select(i => new PriceState { Time = DateTime.Now.AddMinutes(i).Ticks, Last = 100 + i })
    //    .ToList();

    //  var grain = _cluster
    //    .GrainFactory
    //    .GetGrain<IPricesGrainAdapter>(Descriptor);

    //  foreach (var price in priceData)
    //  {
    //    await grain.Send(price);
    //  }

    //  _mockConnector
    //    .Setup(x => x.GetGrain<IPricesGrainAdapter>(It.IsAny<string>(), It.IsAny<string>()))
    //    .Returns(grain);

    //  // Act
    //  var result = await grain.Prices(criteria);

    //  // Assert
    //  Assert.NotNull(result);
    //  Assert.Equal(2, result.Data.Count);
    //  Assert.Equal(priceData.TakeLast(2), result.Data);
    //}
  }
}
