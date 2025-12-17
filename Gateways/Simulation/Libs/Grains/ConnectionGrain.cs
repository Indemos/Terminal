using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using MessagePack;
using Orleans;
using Simulation.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public interface ISimConnectionGrain : IConnectionGrain
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
  public class SimConnectionGrain : ConnectionGrain, ISimConnectionGrain
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
    /// Subscription states
    /// </summary>
    protected ConcurrentDictionary<string, Summary> summaries = new();

    /// <summary>
    /// Subscriptions
    /// </summary>
    protected ConcurrentDictionary<string, Func<Task>> subscriptions = new();

    /// <summary>
    /// Instrument streams
    /// </summary>
    protected ConcurrentDictionary<string, IEnumerator<string>> streams = new();

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
      streams = state.Account.Instruments.ToDictionary(
        o => o.Value.Name,
        o => Directory
          .EnumerateFiles(Path.Combine(state.Source, o.Value.Name), "*", SearchOption.AllDirectories)
          .GetEnumerator())
          .Concurrent();

      await Task.WhenAll(state.Account.Instruments.Values.Select(Subscribe));

      Task.Run(async () =>
      {
        while (streams.Count is not 0)
        {
          foreach (var action in subscriptions.Values)
          {
            await action();
          }
        }
      }).Ignore();

      connections.AddRange(streams.Values);

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
      state?.Account?.Instruments?.Values?.ForEach(o => Unsubscribe(o));

      streams?.Clear();
      summaries?.Clear();
      connections?.Clear();
      subscriptions?.Clear();

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
      await Unsubscribe(instrument);

      var descriptor = this.GetDescriptor();
      var instrumentDescriptor = this.GetDescriptor(instrument.Name);
      var domGrain = GrainFactory.GetGrain<IDomGrain>(instrumentDescriptor);
      var instrumentGrain = GrainFactory.GetGrain<IInstrumentGrain>(instrumentDescriptor);
      var optionsGrain = GrainFactory.GetGrain<IOptionsGrain>(instrumentDescriptor);
      var ordersGrain = GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);
      var positionsGrain = GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      subscriptions[instrument.Name] = async () =>
      {
        var stream = streams.Get(instrument.Name);
        var state = summaries.Get(instrument.Name);

        if (state is null && stream is not null)
        {
          if (stream.MoveNext() is false)
          {
            streams.TryRemove(instrument.Name, out _);
            return;
          }

          summaries[instrument.Name] = GetSummary(instrument.Name, stream.Current);
        }

        var min = summaries.MinBy(o => o.Value?.Instrument?.Price?.Time);

        if (Equals(min.Key, instrument.Name) && summaries.All(o => o.Value is not null))
        {
          var orders = await ordersGrain.Orders(default);
          var positions = await positionsGrain.Positions(default);
          var ordersMap = orders.Data.GroupBy(o => o.Operation.Instrument.Name).ToDictionary(o => o.Key);
          var positionsMap = positions.Data.GroupBy(o => o.Operation.Instrument.Name).ToDictionary(o => o.Key);
          var optionsMap = min.Value.Options.Where(o => ordersMap.ContainsKey(o.Name) || positionsMap.ContainsKey(o.Name));
          var group = await instrumentGrain.Send(min.Value.Instrument with
          {
            Name = instrument.Name,
            TimeFrame = instrument.TimeFrame
          });

          await domGrain.Store(min.Value.Dom);
          await optionsGrain.Store(min.Value.Options);
          await ordersGrain.Tap(group);
          await positionsGrain.Tap(group);

          foreach (var option in optionsMap)
          {
            await ordersGrain.Tap(option);
            await positionsGrain.Tap(option);
          }

          await observer.StreamView(group);
          await observer.StreamTrade(group);

          summaries[instrument.Name] = null;
        }
      };

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Unsubscribe(Instrument instrument)
    {
      subscriptions.TryRemove(instrument.Name, out var subscription);

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Pause
      });
    }

    /// <summary>
    /// Parse snapshot document to get current symbol and options state
    /// </summary>
    /// <param name="name"></param>
    /// <param name="source"></param>
    protected virtual Summary GetSummary(string name, string source)
    {
      var document = new FileInfo(source);

      if (string.Equals(document.Extension, ".bin", StringComparison.InvariantCultureIgnoreCase))
      {
        var content = File.ReadAllBytes(source);

        return MessagePackSerializer.Deserialize<Summary>(content, converter.MessageOptions);
      }

      if (string.Equals(document.Extension, ".zip", StringComparison.InvariantCultureIgnoreCase))
      {
        using (var stream = File.OpenRead(source))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        using (var content = archive.Entries.First().Open())
        {
          return JsonSerializer.Deserialize<Summary>(content, converter.Options);
        }
      }

      var inputMessage = File.ReadAllText(source);

      return JsonSerializer.Deserialize<Summary>(inputMessage, converter.Options);
    }
  }
}
