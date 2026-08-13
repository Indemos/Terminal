using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Topstep.Models;
using TopstepX;
using TopstepX.Models.Gateway;
using TopstepX.Models.Orders;
using TopstepX.SignalR;

namespace Topstep.Grains
{
  public interface ITopstepConnectionGrain : IConnectionGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);
  }

  /// <summary>
  /// Constructor
  /// </summary>
  public class TopstepConnectionGrain : ConnectionGrain, ITopstepConnectionGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Timer
    /// </summary>
    protected IDisposable counter;

    /// <summary>
    /// Connector
    /// </summary>
    protected TopstepBroker connector;

    /// <summary>
    /// Account connection
    /// </summary>
    protected UserHubGateway accountConnection;

    /// <summary>
    /// Instrument connections
    /// </summary>
    protected Dictionary<string, MarketHubGateway> instrumentConnections = new();

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;
      connector = new(connection.Username, connection.Token);

      var descriptor = this.GetDescriptor();
      var response = new StatusResponse();
      var signature = await connector.SignIn();
      var ordersGrain = GrainFactory.GetGrain<ITopstepOrdersGrain>(descriptor);
      var positionsGrain = GrainFactory.GetGrain<ITopstepPositionsGrain>(descriptor);
      var orderSenderGrain = GrainFactory.GetGrain<ITopstepOrderSenderGrain>(descriptor);
      var actionsGrain = GrainFactory.GetGrain<ITopstepTransactionsGrain>(descriptor);

      if (signature.success)
      {
        response = response with { Data = StatusEnum.Active };
      }

      async Task session()
      {
        var scope = await connector.Validate();

        connector.SetAuthHeader(scope.newToken);

        await ordersGrain.Validate(scope.newToken);
        await positionsGrain.Validate(scope.newToken);
        await orderSenderGrain.Validate(scope.newToken);
        await actionsGrain.Validate(scope.newToken);

        foreach (var o in state.Account.Instruments.Values)
        {
          await GrainFactory
            .GetGrain<ITopstepInstrumentGrain>(this.GetDescriptor(o.Name))
            .Validate(scope.newToken);
        }
      }

      await ordersGrain.Setup(connection, observer);
      await positionsGrain.Setup(connection, observer);
      await orderSenderGrain.Setup(connection, observer);
      await actionsGrain.Setup(connection, observer);

      foreach (var o in state.Account.Instruments.Values)
      {
        await GrainFactory
          .GetGrain<ITopstepInstrumentGrain>(this.GetDescriptor(o.Name))
          .Setup(connection, observer);
      }

      accountConnection = connector.CreateUserHubGateway(int.Parse(connection.Account.Descriptor));
      accountConnection.OnOrder += message => observer.StreamOrder(MapOrder(message));

      await session();
      await accountConnection.StartAsync();
      await Task.WhenAll(state.Account.Instruments.Values.Select(Subscribe));

      counter = this.RegisterGrainTimer(async data => await session(), 0, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));

      return response;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override async Task<StatusResponse> Disconnect()
    {
      foreach (var connection in instrumentConnections?.Values)
      {
        await connection.StopAsync();
      }

      if (accountConnection is not null)
      {
        await accountConnection.StopAsync();
      }
      
      instrumentConnections?.Clear();
      connections?.ForEach(o => o.Dispose());
      connections?.Clear();
      connector?.Dispose();
      counter?.Dispose();

      return new()
      {
        Data = StatusEnum.Inactive
      };
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override async Task<StatusResponse> Subscribe(Instrument instrument)
    {
      var descriptor = this.GetDescriptor();
      var instrumentDescriptor = this.GetDescriptor(instrument.Name);
      var instrumentGrain = GrainFactory.GetGrain<IInstrumentGrain>(instrumentDescriptor);
      var instrumentConnection = connector.CreateMarketHubGateway(instrument.Id);

      instrumentConnection.OnQuote += async (message, o) =>
      {
        var group = await instrumentGrain.Send(instrument with
        {
          Price = MapPrice(o)
        });

        await observer.StreamInstrument(group);
      };

      await instrumentConnection.StartAsync();

      instrumentConnections[instrument.Name] = instrumentConnection;

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public override async Task<StatusResponse> Unsubscribe(Instrument instrument)
    {
      if (instrumentConnections.TryGetValue(instrument.Name, out var connection))
      {
        await connection.StopAsync();
        instrumentConnections.Remove(instrument.Name);
      }

      return new()
      {
        Data = StatusEnum.Pause
      };
    }

    /// <summary>
    /// Get price
    /// </summary>
    /// <param name="message"></param>
    protected virtual Price MapPrice(GatewayQuote message) => new()
    {
      Ask = message.bestAsk,
      Bid = message.bestBid,
      Volume = message.volume,
      Last = message.lastPrice,
      Time = DateTime.Now.Ticks
    };

    /// <summary>
    /// Map order
    /// </summary>
    /// <param name="message"></param>
    protected virtual Order MapOrder(GatewayUserOrder message)
    {
      var instrument = new Instrument
      {
        Id = message.contractId,
        Name = message.symbolId,
        Type = InstrumentEnum.Futures
      };

      var action = new Operation
      {
        Amount = message.size,
        Status = OrderStatusEnum.Order,
        Instrument = instrument
      };

      var order = new Order
      {
        Id = $"{message.id}",
        Operation = action,
        Type = OrderTypeEnum.Market,
        Amount = message.size,
        Side = MapSide(message)
      };

      switch (message?.type)
      {
        case OrderType.Limit: order = order with { Type = OrderTypeEnum.Limit, Price = message.limitPrice }; break;
        case OrderType.Stop: order = order with { Type = OrderTypeEnum.Stop, Price = message.stopPrice }; break;
        case OrderType.StopLimit:

          order = order with
          {
            Price = message.limitPrice,
            Type = OrderTypeEnum.StopLimit,
            ActivationPrice = message.stopPrice
          };

          break;
      }

      return order;
    }

    /// <summary>
    /// Map side
    /// </summary>
    /// <param name="message"></param>
    protected virtual OrderSideEnum? MapSide(GatewayUserOrder message)
    {
      switch (message.side)
      {
        case OrderSide.Buy: return OrderSideEnum.Long;
        case OrderSide.Sell: return OrderSideEnum.Short;
      }

      return null;
    }
  }
}
