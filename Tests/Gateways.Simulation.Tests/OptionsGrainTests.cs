using Core.Common.Enums;
using Core.Common.Grains;
using Core.Common.States;
using Core.Common.Tests;
using Moq;
using Orleans;
using Orleans.TestingHost;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Simulation.Options.Tests
{
  public class OptionsGrainTests : IDisposable
  {
    private readonly Mock<IClusterClient> _mockConnector;
    private readonly TestCluster _cluster;

    private string Descriptor => JsonSerializer.Serialize(new DescriptorState
    {
      Account = "Demo"
    });

    public OptionsGrainTests()
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

    [Fact]
    public async Task GetOptions_WithStrikeFilters_ShouldReturnFilteredOptions()
    {
      // Arrange
      var criteria = new MetaState
      {
        Instrument = new InstrumentState { Name = "AAPL" },
        MinPrice = 140,
        MaxPrice = 160
      };

      var optionsData = new List<InstrumentState>
      {
        new() { Name = "AAPL_Call_130", Derivative = new DerivativeState { Strike = 130, Side = OptionSideEnum.Call } },
        new() { Name = "AAPL_Call_150", Derivative = new DerivativeState { Strike = 150, Side = OptionSideEnum.Call } },
        new() { Name = "AAPL_Call_170", Derivative = new DerivativeState { Strike = 170, Side = OptionSideEnum.Call } }
      };

      var grain = _cluster
        .GrainFactory
        .GetGrain<ISimOptionsGrain>(Descriptor);

      _mockConnector
        .Setup(x => x.GetGrain<ISimOptionsGrain>(It.IsAny<string>(), It.IsAny<string>()))
        .Returns(grain);

      await grain.Store(optionsData);

      // Act
      var result = await grain.Options(criteria);

      // Assert
      Assert.NotNull(result);
      Assert.Single(result.Data);
      Assert.Equal(150, result.Data.First().Derivative.Strike);
    }
  }
}
