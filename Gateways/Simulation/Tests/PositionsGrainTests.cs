using Core.Enums;
using Core.Grains;
using Core.Models;
using Core.Tests;
using Moq;
using Orleans;
using Orleans.TestingHost;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Simulation.Prices.Tests
{
  public class Positions : IDisposable
  {
    private readonly Mock<IClusterClient> _mockConnector;
    private readonly TestCluster _cluster;

    private string Descriptor => $"{Guid.NewGuid()}";

    public Positions()
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
    public void StoreException()
    {
      var grain = _cluster
        .GrainFactory
        .GetGrain<IPositionsGrain>(Descriptor);

      var order = new OrderModel();

      Assert.Throws<AggregateException>(() => grain.Store(order).Result);
    }

    [Fact]
    public async Task Store()
    {
      var grain = _cluster.GrainFactory.GetGrain<IPositionsGrain>(Descriptor);
      var order = new OrderModel
      {
        Amount = 1,
        Price = 20,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new OperationModel
        {
          Amount = 1,
          Time = DateTime.Now.Ticks,
          Status = OrderStatusEnum.Position,
          Instrument = new InstrumentModel
          {
            Name = "SPY"
          }
        }
      };

      // Open

      var openPosition = await grain.Store(order);
      var openPositions = await grain.Positions(default);

      Assert.Single(openPositions);
      Assert.Null(openPosition.Transaction);
      Assert.Equal(JsonSerializer.Serialize(order), JsonSerializer.Serialize(openPositions.First()));
      Assert.Equal(JsonSerializer.Serialize(order), JsonSerializer.Serialize(openPosition.Data));

      // Average down

      var averageDownPosition = await grain.Store(order with { Price = 10 });
      var averageDownPositions = await grain.Positions(default);
      var averageDownOrder = order with { Amount = 2, Price = 15, Operation = order.Operation with { Amount = 2, AveragePrice = 15 } };

      Assert.Single(averageDownPositions);
      Assert.Null(averageDownPosition.Transaction);
      Assert.Equal(JsonSerializer.Serialize(averageDownOrder), JsonSerializer.Serialize(averageDownPositions.First()));
      Assert.Equal(JsonSerializer.Serialize(averageDownOrder), JsonSerializer.Serialize(averageDownPosition.Data));

      // Average up

      var averageUpPosition = await grain.Store(order with { Price = 30 });
      var averageUpPositions = await grain.Positions(default);
      var averageUpOrder = order with { Amount = 3, Price = 20, Operation = order.Operation with { Amount = 3, AveragePrice = 20 } };

      Assert.Single(averageUpPositions);
      Assert.Null(averageUpPosition.Transaction);
      Assert.Equal(JsonSerializer.Serialize(averageUpOrder), JsonSerializer.Serialize(averageUpPositions.First()));
      Assert.Equal(JsonSerializer.Serialize(averageUpOrder), JsonSerializer.Serialize(averageUpPosition.Data));

      // Decrease

      var decreasePosition = await grain.Store(order with { Side = OrderSideEnum.Short, Price = 40 });
      var decreasePositions = await grain.Positions(default);
      var decreaseOrder = order with { Amount = 2, Price = 20, Operation = order.Operation with { Amount = 2, AveragePrice = 20 } };
      var decreaseTransaction = order with
      {
        Amount = 1,
        Operation = order.Operation with { Status = OrderStatusEnum.Transaction, Amount = 1, AveragePrice = 20, Price = 40 }
      };

      Assert.Single(decreasePositions);
      Assert.Equal(JsonSerializer.Serialize(decreaseOrder), JsonSerializer.Serialize(decreasePositions.First()));
      Assert.Equal(JsonSerializer.Serialize(decreaseOrder), JsonSerializer.Serialize(decreasePosition.Data));
      Assert.Equal(JsonSerializer.Serialize(decreaseTransaction), JsonSerializer.Serialize(decreasePosition.Transaction));

      // Inverse

      var inversePosition = await grain.Store(order with { Amount = 3, Side = OrderSideEnum.Short, Price = 50 });
      var inversePositions = await grain.Positions(default);
      var inverseOrder = order with
      {
        Amount = 1,
        Price = 50,
        Side = OrderSideEnum.Short,
        Operation = order.Operation with { Amount = 1, AveragePrice = 50 }
      };
      var inverseTransaction = order with
      {
        Amount = 2,
        Operation = order.Operation with { Status = OrderStatusEnum.Transaction, Amount = 2, AveragePrice = 20, Price = 50 }
      };

      Assert.Single(decreasePositions);
      Assert.Equal(JsonSerializer.Serialize(inverseOrder), JsonSerializer.Serialize(inversePositions.First()));
      Assert.Equal(JsonSerializer.Serialize(inverseOrder), JsonSerializer.Serialize(inversePosition.Data));
      Assert.Equal(JsonSerializer.Serialize(inverseTransaction), JsonSerializer.Serialize(inversePosition.Transaction));

      // Average short

      var averageShortPosition = await grain.Store(order with { Amount = 1, Side = OrderSideEnum.Short, Price = 40 });
      var averageShortPositions = await grain.Positions(default);
      var averageShortOrder = order with
      {
        Amount = 2,
        Price = 45,
        Side = OrderSideEnum.Short,
        Operation = order.Operation with { Amount = 2, AveragePrice = 45 }
      };

      Assert.Single(averageShortPositions);
      Assert.Equal(JsonSerializer.Serialize(averageShortOrder), JsonSerializer.Serialize(averageShortPositions.First()));
      Assert.Equal(JsonSerializer.Serialize(averageShortOrder), JsonSerializer.Serialize(averageShortPosition.Data));

      // Close

      var closePosition = await grain.Store(order with { Amount = 2, Price = 5 });
      var closePositions = await grain.Positions(default);
      var closeTransaction = order with
      {
        Amount = 2,
        Price = 45,
        Side = OrderSideEnum.Short,
        Operation = order.Operation with { Status = OrderStatusEnum.Transaction, Amount = 2, AveragePrice = 45, Price = 5 }
      };

      Assert.Empty(closePositions);
      Assert.Null(closePosition.Data);
      Assert.Equal(JsonSerializer.Serialize(closeTransaction), JsonSerializer.Serialize(closePosition.Transaction));
    }
  }
}
