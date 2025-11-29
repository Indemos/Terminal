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
      var currency = instrument.Currency.Name;
      var security = new SharedSymbol(TradingMode.Spot, instrument.Name, currency, instrument?.Derivative?.ExpirationDate);
      var query = new SubscribeBookTickerRequest(security);
      var subResponse = await streamer.SubscribeToBookTickerUpdatesAsync(state.Exchange, query, o => SendStream(instrument, MapPrice(o)));

      if (subResponse.Success is false)
      {
        switch (true)
        {
          case true when Equals(state.Exchange, streamer.Coinbase.Exchange): await Coinbase(instrument); break;
        }
      }

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Stream price
    /// </summary>
    /// <param name="instrument"></param>
    /// <param name="o"></param>
    protected virtual async void SendStream(Instrument instrument, Price o)
    {
      var currency = instrument.Currency.Name;
      var instrumentDescriptor = this.GetDescriptor(instrument.Name + currency);
      var instrumentGrain = GrainFactory.GetGrain<IInstrumentGrain>(instrumentDescriptor);
      var group = await instrumentGrain.Send(instrument with { Price = o });

      observer.StreamPrice(group);

      await observer.StreamInstrument(group);
    }

    /// <summary>
    /// Map book
    /// </summary>
    /// <param name="o"></param>
    protected virtual Price MapPrice(ExchangeEvent<SharedBookTicker> o) => new()
    {
      Bid = (double)o.Data.BestBidPrice,
      BidSize = (double)o.Data.BestBidQuantity,
      Ask = (double)o.Data.BestAskPrice,
      AskSize = (double)o.Data.BestAskQuantity,
      Last = (double)((o.Data.BestBidPrice + o.Data.BestAskPrice) / 2),
      Time = o.DataTime?.Ticks
    };

    /// <summary>
    /// Subscribe to Coinbase
    /// </summary>
    /// <param name="instrument"></param>
    protected virtual Task Coinbase(Instrument instrument)
    {
      var security = new SharedSymbol(
        TradingMode.Spot,
        instrument.Name,
        instrument.Currency.Name,
        instrument?.Derivative?.ExpirationDate);

      var name = streamer.Coinbase.AdvancedTradeApi.FormatSymbol(
        security.BaseAsset,
        security.QuoteAsset,
        security.TradingMode,
        security.DeliverTime);

      return streamer.Coinbase.AdvancedTradeApi.SubscribeToTickerUpdatesAsync(name, o =>
      {
        SendStream(instrument, new()
        {
          Bid = (double)o.Data.BestBidPrice,
          BidSize = (double)o.Data.BestBidQuantity,
          Ask = (double)o.Data.BestAskPrice,
          AskSize = (double)o.Data.BestAskQuantity,
          Last = (double)((o.Data.BestBidPrice + o.Data.BestAskPrice) / 2),
          Time = o.DataTime?.Ticks
        });
      });
    }
  }
}
