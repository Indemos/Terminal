using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Grains;
using Core.Common.Services;
using Core.Common.States;
using MessagePack;
using MessagePack.Resolvers;
using Orleans;
using Orleans.Streams;
using Simulation.States;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public class ConnectionGrain : Grain<ConnectionState>, IGrainWithStringKey
  {
    /// <summary>
    /// Timer broadcasting quotes
    /// </summary>
    protected IDisposable interval;

    /// <summary>
    /// Quote stream
    /// </summary>
    protected IAsyncStream<PriceState> dataStream;

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
    protected ConcurrentDictionary<string, Action> subscriptions = new();

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
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override Task OnActivateAsync(CancellationToken cancellation)
    {
      dataStream = this
        .GetStreamProvider(nameof(StreamEnum.Price))
        .GetStream<PriceState>(this.GetPrimaryKeyString(), Guid.Empty);

      return base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Set state
    /// </summary>
    /// <param name="instruments"></param>
    /// <returns></returns>
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

      await Task.WhenAll(State.Instruments.Values.Select(Subscribe));

      interval = this.RegisterGrainTimer(
        cancellation =>
        {
          subscriptions.Values.ForEach(o => o());
          return Task.CompletedTask;
        },
        TimeSpan.FromMicroseconds(0),
        TimeSpan.FromMicroseconds(speed));

      connections.Add(interval);

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
      await Task.WhenAll(State.Instruments.Values.Select(Unsubscribe));

      connections?.ForEach(o => o?.Dispose());
      connections?.Clear();

      return new StatusResponse
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public async Task<StatusResponse> Subscribe(InstrumentState instrument)
    {
      var descriptor = this.GetPrimaryKeyString();
      var domGrain = GrainFactory.GetGrain<DomGrain>($"{descriptor}:{instrument.Name}");
      var pricesGrain = GrainFactory.GetGrain<PricesGrain>($"{descriptor}:{instrument.Name}");
      var optionsGrain = GrainFactory.GetGrain<OptionsGrain>($"{descriptor}:{instrument.Name}");

      await Unsubscribe(instrument);

      subscriptions[instrument.Name] = () =>
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
            Instrument = instrument,
            TimeFrame = instrument.TimeFrame
          };

          pricesGrain.Store(point);
          domGrain.Store(next.Value.Dom);
          optionsGrain.Store(next.Value.Options);

          summaries[instrument.Name] = summaries[instrument.Name] with { Status = StatusEnum.Pause };

          dataStream.OnNextAsync(point);
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
        Data = StatusEnum.Active
      });
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
