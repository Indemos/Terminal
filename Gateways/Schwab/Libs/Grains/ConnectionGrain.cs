using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using Orleans;
using Schwab.Enums;
using Schwab.Messages;
using Schwab.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Schwab.Grains
{
  public interface ISchwabConnectionGrain : IConnectionGrain
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
  public class SchwabConnectionGrain : ConnectionGrain, ISchwabConnectionGrain
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
    /// Timer
    /// </summary>
    protected IDisposable counter;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;
      connector = new SchwabBroker()
      {
        ClientId = connection.Id,
        ClientSecret = connection.Secret,
        AccessToken = connection.AccessToken,
        RefreshToken = connection.RefreshToken
      };

      var scope = await connector.Authenticate();
      var cts = new CancellationTokenSource(connection.Timeout);
      var account = await connector.GetAccountCode(cts.Token);

      async Task session()
      {
        var descriptor = this.GetDescriptor();
        var scope = await connector.Authenticate();

        connection = connection with { AccessToken = scope?.AccessToken };

        await GrainFactory.GetGrain<ISchwabOrdersGrain>(descriptor).Setup(connection, observer);
        await GrainFactory.GetGrain<ISchwabPositionsGrain>(descriptor).Setup(connection, observer);
        await GrainFactory.GetGrain<ISchwabOrderSenderGrain>(descriptor).Setup(connection, observer);
        await GrainFactory.GetGrain<ISchwabTransactionsGrain>(descriptor).Setup(connection, observer);

        foreach (var o in state.Account.Instruments.Values)
        {
          await GrainFactory.GetGrain<ISchwabOptionsGrain>(this.GetDescriptor(o.Name)).Setup(connection, observer);
          await GrainFactory.GetGrain<ISchwabInstrumentGrain>(this.GetDescriptor(o.Name)).Setup(connection, observer);
        }
      }

      connection = connection with
      {
        AccessToken = scope?.AccessToken,
        Account = connection.Account with { Descriptor = account?.FirstOrDefault()?.HashValue }
      };

      await session();
      await connector.Stream(CancellationToken.None);
      await Task.WhenAll(connection.Account.Instruments.Values.Select(Subscribe));

      counter = this.RegisterGrainTimer(async data => await session(), 0, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Disconnect
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      connections?.ForEach(o => o.Dispose());
      connections?.Clear();
      connector?.Dispose();
      counter?.Dispose();

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

      await connector.Subscribe(instrument.Name, MapSubType(instrument), async o =>
      {
        var group = await instrumentGrain.Send(instrument with
        {
          Price = MapPrice(o)
        });

        await observer.StreamInstrument(group.Data);
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
      //Bids = [.. message.Bids.Select(MapPrice)],
      //Asks = [.. message.Asks.Select(MapPrice)]
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
