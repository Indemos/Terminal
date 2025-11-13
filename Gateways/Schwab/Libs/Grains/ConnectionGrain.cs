using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using Core.Services;
using Schwab.Enums;
using Schwab.Messages;
using Schwab.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Schwab.Grains
{
  public interface ISchwabConnectionGrain : IConnectionGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(Connection connection);
  }

  /// <summary>
  /// Constructor
  /// </summary>
  /// <param name="messenger"></param>
  public class SchwabConnectionGrain(MessageService messenger) : ConnectionGrain(messenger), ISchwabConnectionGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected SchwabBroker connector;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection)
    {
      await Disconnect();

      state = connection;
      connector = new()
      {
        ClientId = connection.Id,
        ClientSecret = connection.Secret,
        AccessToken = connection.AccessToken,
        RefreshToken = connection.RefreshToken
      };

      await connector.Connect();
      await Task.WhenAll(connection.Account.Instruments.Values.Select(Subscribe));

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      connections?.ForEach(o => o.Dispose());
      connections?.Clear();
      connector?.Dispose();

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Inactive
      });
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override async Task<StatusResponse> Subscribe(Instrument instrument)
    {
      var descriptor = this.GetDescriptor();
      var instrumentDescriptor = this.GetDescriptor(instrument.Name);
      var domGrain = GrainFactory.GetGrain<IDomGrain>(instrumentDescriptor);
      var instrumentGrain = GrainFactory.GetGrain<IInstrumentGrain>(instrumentDescriptor);
      var positionsGrain = GrainFactory.GetGrain<IPositionsGrain>(descriptor);
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(descriptor);

      await connector.Subscribe(instrument.Name, MapSubType(instrument), async o =>
      {
        var message = await instrumentGrain.Send(instrument with
        {
          Price = MapPrice(o)
        });

        await ordersGrain.Tap(message);
        await positionsGrain.Tap(message);
        await messenger.Send(message);
      });

      await connector.SubscribeToDom(instrument.Name, MapDomSubType(instrument), async o =>
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
    protected virtual Price MapPrice(PriceMessage message) => new()
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
    protected virtual Dom MapDom(DomMessage message) => new()
    {
      Bids = [.. message.Bids.Select(MapPrice)],
      Asks = [.. message.Asks.Select(MapPrice)]
    };

    /// <summary>
    /// Get subscription type
    /// </summary>
    /// <param name="instrument"></param>
    protected virtual Schwab.Enums.SubscriptionEnum MapSubType(Instrument instrument)
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
    protected virtual DomEnum MapDomSubType(Instrument instrument)
    {
      return instrument.Type is InstrumentEnum.Options ?
        DomEnum.OPTIONS_BOOK : 
        DomEnum.NASDAQ_BOOK;
    }
  }
}
