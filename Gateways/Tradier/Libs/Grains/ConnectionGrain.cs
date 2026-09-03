using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tradier.Messages.Stream;
using Tradier.Models;

namespace Tradier.Grains
{
  public interface ITradierConnectionGrain : IConnectionGrain
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
  public class TradierConnectionGrain : ConnectionGrain, ITradierConnectionGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected TradierBroker connector;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      var descriptor = this.GetDescriptor();
      var cts = new CancellationTokenSource(connection.Timeout);

      state = connection;
      observer = grainObserver;
      connector = new()
      {
        Token = connection.AccessToken,
        SessionToken = connection.SessionToken,
      };

      await GrainFactory.GetGrain<ITradierOrdersGrain>(descriptor).Setup(connection, observer);
      await GrainFactory.GetGrain<ITradierPositionsGrain>(descriptor).Setup(connection, observer);
      await GrainFactory.GetGrain<ITradierOrderSenderGrain>(descriptor).Setup(connection, observer);
      await GrainFactory.GetGrain<ITradierTransactionsGrain>(descriptor).Setup(connection, observer);

      foreach (var o in state.Account.Instruments.Values)
      {
        await GrainFactory.GetGrain<ITradierOptionsGrain>(this.GetDescriptor(o.Name)).Setup(connection, observer);
        await GrainFactory.GetGrain<ITradierInstrumentGrain>(this.GetDescriptor(o.Name)).Setup(connection, observer);
      }

      await connector.Connect(cts.Token);
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
      var instrumentGrain = GrainFactory.GetGrain<IInstrumentGrain>(instrumentDescriptor);

      connector.OnPrice += async o =>
      {
        var group = await instrumentGrain.Send(instrument with
        {
          Price = MapPrice(o)
        });

        await observer.StreamInstrument(group.Data);
      };

      await connector.Subscribe(instrument.Name);

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
      Last = (message.Bid + message.Ask) / 2.0,
      Time = message?.BidDate ?? DateTime.Now.Ticks
    };
  }
}
