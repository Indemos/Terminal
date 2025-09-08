using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Grains;
using Core.Common.Services;
using Core.Common.States;
using MessagePack;
using MessagePack.Resolvers;
using Orleans;
using Simulation.States;
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
  public interface IConnectionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="source"></param>
    /// <param name="speed"></param>
    Task<StatusResponse> Connect(string source, int speed);

    /// <summary>
    /// Save state and dispose
    /// </summary>
    Task<StatusResponse> Disconnect();

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Subscribe(InstrumentState instrument);

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Unsubscribe(InstrumentState instrument);

    /// <summary>
    /// Set state
    /// </summary>
    /// <param name="instruments"></param>
    Task StoreInstruments(Dictionary<string, InstrumentState> instruments);
  }

  public class ConnectionGrain : Grain<ConnectionState>, IConnectionGrain
  {
    /// <summary>
    /// Timer broadcasting quotes
    /// </summary>
    protected IDisposable interval;

    /// <summary>
    /// HTTP service
    /// </summary>
    protected ConversionService sender = new();

    /// <summary>
    /// Disposable connections
    /// </summary>
    protected List<IDisposable> connections = new();

    /// <summary>
    /// Subscription states
    /// </summary>
    protected ConcurrentDictionary<string, RecordState> summaries = new();

    /// <summary>
    /// Subscriptions
    /// </summary>
    protected ConcurrentDictionary<string, Func<Task>> subscriptions = new();

    /// <summary>
    /// Instrument streams
    /// </summary>
    protected ConcurrentDictionary<string, IEnumerator<string>> streams = new();

    /// <summary>
    /// Message pack options
    /// </summary>
    protected MessagePackSerializerOptions messageOptions = MessagePackSerializerOptions
      .Standard
      .WithResolver(ContractlessStandardResolver.Instance);

    /// <summary>
    /// Set state
    /// </summary>
    /// <param name="instruments"></param>
    public Task StoreInstruments(Dictionary<string, InstrumentState> instruments)
    {
      State = State with
      {
        Instruments = instruments
      };

      return Task.CompletedTask;
    }

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="source"></param>
    /// <param name="speed"></param>
    public async Task<StatusResponse> Connect(string source, int speed)
    {
      await Disconnect();

      streams = State
        .Instruments
        .ToDictionary(
          o => o.Value.Name,
          o => Directory
            .EnumerateFiles(Path.Combine(source, o.Value.Name), "*", SearchOption.AllDirectories)
            .GetEnumerator())
            .Concurrent();

      streams.ForEach(o => connections.Add(o.Value));

      Run(speed);

      await Task.WhenAll(State.Instruments.Values.Select(Subscribe));

      return new StatusResponse
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public async Task<StatusResponse> Disconnect()
    {
      Stop();

      await Task.WhenAll(State.Instruments.Values.Select(Unsubscribe));

      connections?.ForEach(o => o?.Dispose());
      connections?.Clear();

      return new StatusResponse
      {
        Data = StatusEnum.Inactive
      };
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public async Task<StatusResponse> Subscribe(InstrumentState instrument)
    {
      var converter = InstanceService<ConversionService>.Instance;
      var baseDescriptor = converter.Decompose<Descriptor>(this.GetPrimaryKeyString());
      var instrumentDescriptor = new InstrumentDescriptor
      {
        Account = baseDescriptor.Account,
        Instrument = instrument.Name
      };

      var domGrain = GrainFactory.Get<IDomGrain>(instrumentDescriptor);
      var priceGrain = GrainFactory.Get<IPriceGrain>(instrumentDescriptor);
      var optionsGrain = GrainFactory.Get<IOptionsGrain>(instrumentDescriptor);

      await Unsubscribe(instrument);

      subscriptions[instrument.Name] = async () =>
      {
        streams.TryGetValue(instrument.Name, out var stream);
        summaries.TryGetValue(instrument.Name, out var state);

        if (stream is not null && (state is null || state.Status is StatusEnum.Pause))
        {
          stream.MoveNext();

          switch (string.IsNullOrEmpty(stream.Current))
          {
            case true:
              summaries[instrument.Name] = summaries[instrument.Name] with { Status = StatusEnum.Inactive };
              break;

            case false:
              summaries[instrument.Name] = GetSummary(instrument.Name, stream.Current);
              summaries[instrument.Name] = summaries[instrument.Name] with { Status = StatusEnum.Active };
              break;
          }
        }

        var next = summaries.First();

        summaries.ForEach(o => next = o.Value.Instrument.Price.Time <= next.Value.Instrument.Price.Time ? o : next);

        if (Equals(next.Key, instrument.Name))
        {
          var point = next.Value.Instrument.Price with
          {
            Bar = null,
            Name = next.Key,
            TimeFrame = instrument.TimeFrame
          };

          await domGrain.Store(next.Value.Dom);
          await optionsGrain.Store(next.Value.Options);
          await priceGrain.Store(point);

          summaries[instrument.Name] = summaries[instrument.Name] with { Status = StatusEnum.Pause };
        }
      };

      return new StatusResponse
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public Task<StatusResponse> Unsubscribe(InstrumentState instrument)
    {
      subscriptions.TryRemove(instrument.Name, out var subscription);

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Pause
      });
    }

    /// <summary>
    /// Start timer
    /// </summary>
    /// <param name="speed"></param>
    protected void Run(int speed)
    {
      interval = this.RegisterGrainTimer(
        async cancellation => await Task.WhenAll(subscriptions.Values.Select(o => o())),
        TimeSpan.FromMicroseconds(0),
        TimeSpan.FromMicroseconds(speed));
    }

    /// <summary>
    /// Stop timer
    /// </summary>
    protected void Stop()
    {
      interval?.Dispose();
    }

    /// <summary>
    /// Parse snapshot document to get current symbol and options state
    /// </summary>
    /// <param name="name"></param>
    /// <param name="source"></param>
    protected RecordState GetSummary(string name, string source)
    {
      var document = new FileInfo(source);

      if (string.Equals(document.Extension, ".bin", StringComparison.InvariantCultureIgnoreCase))
      {
        var content = File.ReadAllBytes(source);

        return MessagePackSerializer.Deserialize<RecordState>(content, messageOptions);
      }

      if (string.Equals(document.Extension, ".zip", StringComparison.InvariantCultureIgnoreCase))
      {
        using (var stream = File.OpenRead(source))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        using (var content = archive.Entries.First().Open())
        {
          return JsonSerializer.Deserialize<RecordState>(content, sender.Options);
        }
      }

      var inputMessage = File.ReadAllText(source);

      return JsonSerializer.Deserialize<RecordState>(inputMessage, sender.Options);
    }
  }
}
