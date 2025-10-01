using Core.Enums;
using Core.Grains;
using Core.Models;
using Moq;
using Orleans;
using Orleans.TestingHost;
using Simulation.Tests;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Simulation.Prices.Tests
{
  public class Orders : IDisposable
  {
    private readonly Mock<IClusterClient> _mockConnector;
    private readonly TestCluster _cluster;

    private string Descriptor => JsonSerializer.Serialize(new DescriptorModel
    {
      Account = $"{Guid.NewGuid()}"
    });

    public Orders()
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
        .GetGrain<IOrdersGrain>(Descriptor);

      var order = new OrderModel();

      Assert.Throws<AggregateException>(() => grain.Store(order).Result);
    }

    [Fact]
    public async Task StoreUpdatesMarketOrders()
    {
      var descriptor = Descriptor;
      var grain = _cluster.GrainFactory.GetGrain<IOrdersGrain>(descriptor);
      var order = new OrderModel
      {
        Amount = 1.0,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new OperationModel
        {
          Instrument = new InstrumentModel
          {
            Name = "SPY"
          }
        }
      };

      var copyId = $"{Guid.NewGuid()}";

      await grain.Store(order);
      await grain.Store(order with { Id = copyId });
      await grain.Remove(order with { Id = copyId });

      var orders = await grain.Orders(default);
      var orderExpectation = JsonSerializer.Serialize(order with
      {
        Operation = order.Operation with { Status = OrderStatusEnum.Order }
      });

      Assert.Single(orders);
      Assert.Equal(orderExpectation, JsonSerializer.Serialize(orders.First()));

      await grain.Tap(new() { Name = order.Operation.Instrument.Name });

      Assert.Empty(await grain.Orders(default));
    }
  }
}
