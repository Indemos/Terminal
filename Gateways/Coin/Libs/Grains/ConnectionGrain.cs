using Coin.Models;
using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using CryptoClients.Net;
using CryptoClients.Net.Interfaces;
using CryptoExchange.Net.SharedApis;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Coin.Grains
{
  public interface ICoinConnectionGrain : IConnectionGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="observer"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver observer);
  }

  /// <summary>
  /// Constructor
  /// </summary>
  public class CoinConnectionGrain : ConnectionGrain, ICoinConnectionGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Observer
    /// </summary>
    protected ITradeObserver observer;

    /// <summary>
    /// Streamer
    /// </summary>
    protected IExchangeSocketClient streamer;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      var cleaner = new CancellationTokenSource(connection.Timeout);

      await Disconnect();

      state = connection;
      observer = grainObserver;
      streamer = new ExchangeSocketClient();

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
      var currency = instrument.Currency.Name;
      var instrumentDescriptor = this.GetDescriptor(instrument.Name + currency);
      var domGrain = GrainFactory.GetGrain<IDomGrain>(instrumentDescriptor);
      var instrumentGrain = GrainFactory.GetGrain<IInstrumentGrain>(instrumentDescriptor);
      var positionsGrain = GrainFactory.GetGrain<IPositionsGrain>(descriptor);
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(descriptor);
      var symbol = new SharedSymbol(TradingMode.Spot, instrument.Name, currency);
      var subResponse = await streamer.SubscribeToBookTickerUpdatesAsync(state.Exchange, new SubscribeBookTickerRequest(symbol), async o =>
      {
        var group = await instrumentGrain.Send(instrument with { Price = MapBook(o) });

        observer.StreamPrice(group);
        await observer.StreamInstrument(group);
      });

      if (subResponse.Success is false)
      {
        await streamer.SubscribeToTickerUpdatesAsync(state.Exchange, new SubscribeTickerRequest(symbol), async o =>
        {
          var group = await instrumentGrain.Send(instrument with { Price = MapPrice(o) });

          observer.StreamPrice(group);
          await observer.StreamInstrument(group);
        });
      }

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Map book
    /// </summary>
    /// <param name="o"></param>
    protected virtual Price MapBook(ExchangeEvent<SharedBookTicker> o) => new()
    {
      Bid = (double)o.Data.BestBidPrice,
      BidSize = (double)o.Data.BestBidQuantity,
      Ask = (double)o.Data.BestAskPrice,
      AskSize = (double)o.Data.BestAskQuantity,
      Time = DateTime.Now.Ticks
    };

    /// <summary>
    /// Map price
    /// </summary>
    /// <param name="o"></param>
    protected virtual Price MapPrice(ExchangeEvent<SharedSpotTicker> o) => new()
    {
      Bid = (double)o.Data.LastPrice,
      BidSize = (double)o.Data.LastPrice,
      Ask = (double)o.Data.QuoteVolume,
      AskSize = (double)o.Data.QuoteVolume,
      Time = DateTime.Now.Ticks
    };
  }
}
