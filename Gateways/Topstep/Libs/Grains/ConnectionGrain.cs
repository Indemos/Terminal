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
    /// <param name="observer"></param>
    Task<DescriptorResponse> Setup(Connection connection, ITradeObserver observer);
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
    /// Observer
    /// </summary>
    protected ITradeObserver observer;

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
    public virtual async Task<DescriptorResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      await Disconnect();

      state = connection;
      observer = grainObserver;
      connector = new(connection.Username, connection.Token);

      var response = new DescriptorResponse();
      var signature = await connector.SignIn();

      if (signature.success)
      {
        response = response with { Data = signature.token };
      }

      accountConnection = connector.CreateUserHubGateway(int.Parse(connection.Account.Descriptor));
      accountConnection.OnOrder += message => observer.StreamOrder(MapOrder(message));

      var descriptor = this.GetDescriptor();
      var ordersGrain = GrainFactory.GetGrain<ITopstepOrdersGrain>(descriptor);
      var positionsGrain = GrainFactory.GetGrain<ITopstepPositionsGrain>(descriptor);
      var orderSenderGrain = GrainFactory.GetGrain<ITopstepOrderSenderGrain>(descriptor);
      var transactionsGrain = GrainFactory.GetGrain<ITopstepTransactionsGrain>(descriptor);

      await ordersGrain.Setup(connection);
      await positionsGrain.Setup(connection);
      await orderSenderGrain.Setup(connection);
      await transactionsGrain.Setup(connection, observer);

      counter = this.RegisterGrainTimer(async data =>
      {
        var scope = await connector.Validate();

        connector.SetAuthHeader(scope.newToken);

        await ordersGrain.Validate(scope.newToken);
        await positionsGrain.Validate(scope.newToken);
        await orderSenderGrain.Validate(scope.newToken);
        await transactionsGrain.Validate(scope.newToken);

      }, 0, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));

      await Task.WhenAll(state.Account.Instruments.Values.Select(Subscribe));

      return response;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override async Task<StatusResponse> Disconnect()
    {
      if (accountConnection is not null)
      {
        await accountConnection.StopAsync();
      }

      foreach (var connection in instrumentConnections.Values)
      {
        await connection.StopAsync();
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
      var dataConnection = connector.CreateMarketHubGateway(instrument.Id);

      dataConnection.OnQuote += async (message, o) =>
      {
        var group = await instrumentGrain.Send(instrument with
        {
          Price = MapPrice(o)
        });

        await observer.StreamInstrument(group);
      };

      return new()
      {
        Data = StatusEnum.Active
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
        Id = $"{message.id}",
        Amount = message.size,
        Status = OrderStatusEnum.Order,
        Instrument = instrument
      };

      var order = new Order
      {
        Id = message.customTag,
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
