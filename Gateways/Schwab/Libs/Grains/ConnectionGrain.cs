using Core.Enums;
using Core.Grains;
using Core.Models;
using Core.Services;
using Orleans;
using Schwab.Enums;
using Schwab.Messages;
using Schwab.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Schwab.Grains
{
  public interface ISchwabConnectionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Connect(ConnectionModel connection);

    /// <summary>
    /// Disconnect
    /// </summary>
    Task<StatusResponse> Disconnect();

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Subscribe(InstrumentModel instrument);
  }

  /// <summary>
  /// Constructor
  /// </summary>
  /// <param name="messenger"></param>
  public class SchwabConnectionGrain(MessageService messenger) : Grain<ConnectionModel>, ISchwabConnectionGrain
  {
    /// <summary>
    /// Messenger
    /// </summary>
    protected SchwabBroker broker;

    /// <summary>
    /// Messenger
    /// </summary>
    protected MessageService messenger = messenger;

    /// <summary>
    /// HTTP service
    /// </summary>
    protected ConversionService converter = new();

    /// <summary>
    /// Disposable connections
    /// </summary>
    protected List<IDisposable> connections = new();

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Connect(ConnectionModel connection)
    {
      await Disconnect();

      State = connection;

      broker = new()
      {
        ClientId = connection.Id,
        ClientSecret = connection.Secret,
        AccessToken = connection.AccessToken,
        RefreshToken = connection.RefreshToken
      };

      await broker.Connect();
      await Task.WhenAll(connection.Account.Instruments.Values.Select(Subscribe));

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public virtual Task<StatusResponse> Disconnect()
    {
      connections?.ForEach(o => o.Dispose());
      connections?.Clear();
      broker?.Dispose();

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Inactive
      });
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public virtual async Task<StatusResponse> Subscribe(InstrumentModel instrument)
    {
      var descriptor = this.GetPrimaryKeyString();
      var instrumentDescriptor = $"{descriptor}:{instrument.Name}";
      var domGrain = GrainFactory.GetGrain<IDomGrain>(instrumentDescriptor);
      var instrumentGrain = GrainFactory.GetGrain<IInstrumentGrain>(instrumentDescriptor);
      var positionsGrain = GrainFactory.GetGrain<IPositionsGrain>(descriptor);
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(descriptor);

      await broker.Subscribe(instrument.Name, MapSubType(instrument), async o =>
      {
        var message = await instrumentGrain.Store(instrument with
        {
          Price = MapPrice(o)
        });

        await ordersGrain.Tap(message);
        await positionsGrain.Tap(message);
        await messenger.Send(message);
      });

      await broker.SubscribeToDom(instrument.Name, MapDomSubType(instrument), async o =>
      {
        await domGrain.Store(MapDom(o));
      });

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Get price
    /// </summary>
    /// <param name="message"></param>
    protected virtual PriceModel MapPrice(PriceMessage message) => new PriceModel
    {
      Ask = message.Ask,
      Bid = message.Bid,
      AskSize = message.AskSize,
      BidSize = message.BidSize,
      Last = message.Last,
      Time = message.Time,
    };

    /// <summary>
    /// Get price
    /// </summary>
    /// <param name="message"></param>
    protected virtual DomModel MapDom(DomMessage message) => new DomModel
    {
      Bids = [.. message.Bids.Select(MapPrice)],
      Asks = [.. message.Asks.Select(MapPrice)]
    };

    /// <summary>
    /// Get subscription type
    /// </summary>
    /// <param name="instrument"></param>
    protected virtual Schwab.Enums.SubscriptionEnum MapSubType(InstrumentModel instrument)
    {
      switch (instrument.Type)
      {
        case InstrumentEnum.Options: return Schwab.Enums.SubscriptionEnum.LEVELONE_OPTIONS;
        case InstrumentEnum.Futures: return Schwab.Enums.SubscriptionEnum.LEVELONE_FUTURES;
        case InstrumentEnum.Currencies: return Schwab.Enums.SubscriptionEnum.LEVELONE_FOREX;
        case InstrumentEnum.FutureOptions: return Schwab.Enums.SubscriptionEnum.LEVELONE_FUTURES_OPTIONS;
      }

      return Schwab.Enums.SubscriptionEnum.LEVELONE_EQUITIES;
    }

    /// <summary>
    /// Get subscription type
    /// </summary>
    /// <param name="instrument"></param>
    protected virtual Schwab.Enums.DomEnum MapDomSubType(InstrumentModel instrument)
    {
      return instrument.Type is InstrumentEnum.Options ?
        DomEnum.OPTIONS_BOOK : 
        DomEnum.NASDAQ_BOOK;
    }
  }
}
