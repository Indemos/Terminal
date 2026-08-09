using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using Orleans;
using Simulation.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public interface ISimConnectionGrain : IConnectionGrain
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
  public class SimConnectionGrain : ConnectionGrain, ISimConnectionGrain
  {
    protected Connection state;
    protected ITradeObserver observer;
    protected CancellationTokenSource cts;
    protected PriorityQueue<SimStream, long> queue = new();
    protected ConcurrentDictionary<string, SimStream> docs = new();
    protected ConcurrentDictionary<string, SimStream> streams = new();

    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      await Disconnect();

      state = connection;
      observer = grainObserver;
      cts = new CancellationTokenSource();

      foreach (var instrument in state.Account.Instruments.Values)
      {
        var source = Path.Combine(state.Source, $"{instrument.Name}.db");
        var stream = docs[instrument.Name] = new SimStream(source, instrument.Name);

        await Subscribe(instrument);
      }

      connections.Add(this.RegisterGrainTimer(async o => await Process(), 0, TimeSpan.Zero, TimeSpan.FromMicroseconds(1)));

      return new StatusResponse { Data = StatusEnum.Active };
    }

    /// <summary>
    /// Sequential reader loop
    /// </summary>
    private async Task Process()
    {
      try
      {
        var queueResponse = queue.TryDequeue(out var stream, out var _);
        var streamResponse = streams.ContainsKey(stream?.Name ?? string.Empty);

        if (queueResponse is false || streamResponse is false)
        {
          return;
        }

        var descriptor = this.GetDescriptor();
        var instrument = state.Account.Instruments[stream.Name];
        var instrumentDescriptor = this.GetDescriptor(instrument.Name);

        var domGrain = GrainFactory.GetGrain<IDomGrain>(instrumentDescriptor);
        var instrumentGrain = GrainFactory.GetGrain<ISimInstrumentGrain>(instrumentDescriptor);
        var optionsGrain = GrainFactory.GetGrain<IOptionsGrain>(instrumentDescriptor);
        var ordersGrain = GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
        var positionsGrain = GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

        var summary = stream.Current;
        var orders = await ordersGrain.Orders(default);
        var positions = await positionsGrain.Positions(default);
        var ordersMap = orders.Data.GroupBy(o => o.Operation.Instrument.Name).ToDictionary(o => o.Key);
        var positionsMap = positions.Data.GroupBy(o => o.Operation.Instrument.Name).ToDictionary(o => o.Key);
        var optionsMap = summary.Options.Where(o => ordersMap.ContainsKey(o.Name) || positionsMap.ContainsKey(o.Name));

        var group = await instrumentGrain.Send(summary.Instrument with
        {
          Name = instrument.Name,
          TimeFrame = instrument.TimeFrame
        });

        await domGrain.Store(summary.Dom);
        await optionsGrain.Store(summary.Options);
        await ordersGrain.Tap(group);
        await positionsGrain.Tap(group);

        foreach (var option in optionsMap)
        {
          await ordersGrain.Tap(option);
          await positionsGrain.Tap(option);
        }

        await observer.StreamInstrument(group);

        if (stream.MoveNext())
        {
          queue.Enqueue(stream, stream.Current.Time);
        }
      }
      catch (Exception) { }
    }

    /// <summary>
    /// Disconnect
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      cts?.Cancel();
      cts?.Dispose();
      streams?.Clear();
      docs?.Values?.ForEach(o => o.Dispose());
      docs?.Clear();

      return Task.FromResult(new StatusResponse { Data = StatusEnum.Inactive });
    }

    /// <summary>
    /// Subscribe
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Subscribe(Instrument instrument)
    {
      if (streams.ContainsKey(instrument.Name) is false)
      {
        var stream = docs[instrument.Name];

        if (stream.MoveNext())
        {
          queue.Enqueue(streams[instrument.Name] = stream, stream.Current.Time);
        }
      }

      return Task.FromResult(new StatusResponse { Data = StatusEnum.Active });
    }

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Unsubscribe(Instrument instrument)
    {
      streams.TryRemove(instrument.Name, out var _);

      return Task.FromResult(new StatusResponse { Data = StatusEnum.Pause });
    }
  }
}
