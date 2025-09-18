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
  public interface IConnectionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="state"></param>
    Task<StatusResponse> Connect(ConnectionState state);

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
  }

  public class ConnectionGrain : Grain<ConnectionState>, IConnectionGrain
  {
    /// <summary>
    /// Published price
    /// </summary>
    protected PriceState pubPrice;

    /// <summary>
    /// Subscribed price
    /// </summary>
    protected PriceState subPrice;

    /// <summary>
    /// Descriptor
    /// </summary>
    protected DescriptorState descriptor;

    /// <summary>
    /// Data stream
    /// </summary>
    protected IAsyncStream<PriceState> dataStream;

    /// <summary>
    /// HTTP service
    /// </summary>
    protected ConversionService converter = new();

    /// <summary>
    /// Disposable connections
    /// </summary>
    protected List<IDisposable> connections = new();

    /// <summary>
    /// Subscription states
    /// </summary>
    protected ConcurrentDictionary<string, SummaryState> summaries = new();

    /// <summary>
    /// Subscriptions
    /// </summary>
    protected ConcurrentDictionary<string, Func<Task>> subscriptions = new();

    /// <summary>
    /// Instrument streams
    /// </summary>
    protected ConcurrentDictionary<string, IEnumerator<string>> streams = new();

    /// <summary>
    /// Instruments
    /// </summary>
    protected ConcurrentDictionary<string, InstrumentState> instruments = new();

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
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<DescriptorState>(this.GetPrimaryKeyString());

      dataStream = this
        .GetStreamProvider(nameof(StreamEnum.Price))
        .GetStream<PriceState>(descriptor.Account, Guid.Empty);

      await dataStream.SubscribeAsync((o, x) => Task.FromResult(subPrice = o));
      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="state"></param>
    public virtual async Task<StatusResponse> Connect(ConnectionState state)
    {
      State = state;
      streams = (instruments = State.Instruments.Concurrent()).ToDictionary(
        o => o.Value.Name,
        o => Directory
          .EnumerateFiles(Path.Combine(State.Source, o.Value.Name), "*", SearchOption.AllDirectories)
          .GetEnumerator())
          .Concurrent();

      await Task.WhenAll(instruments.Values.Select(Subscribe));

      var counter = this.RegisterGrainTimer(
        o => Task.WhenAll(subscriptions.Values.Select(sub => sub())),
        TimeSpan.FromMicroseconds(0),
        TimeSpan.FromMicroseconds(State.Speed));

      connections.AddRange(streams.Values);
      connections.Add(counter);

      return new StatusResponse
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public virtual async Task<StatusResponse> Disconnect()
    {
      await Task.WhenAll(instruments.Values.Select(Unsubscribe));

      connections?.ForEach(o => o?.Dispose());

      streams?.Clear();
      summaries?.Clear();
      instruments?.Clear();
      connections?.Clear();
      subscriptions?.Clear();

      return new StatusResponse
      {
        Data = StatusEnum.Inactive
      };
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public virtual async Task<StatusResponse> Subscribe(InstrumentState instrument)
    {
      await Unsubscribe(instrument);

      var instrumentDescriptor = descriptor with { Instrument = instrument.Name };
      var domGrain = GrainFactory.Get<IDomGrain>(instrumentDescriptor);
      var pricesGrain = GrainFactory.Get<ISimPricesGrain>(instrumentDescriptor);
      var optionsGrain = GrainFactory.Get<IOptionsGrain>(instrumentDescriptor);

      subscriptions[instrument.Name] = async () =>
      {
        if (pubPrice is null || Equals(pubPrice, subPrice))
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
          var empties = summaries.Any(o => o.Value is null);

          if (Equals(min.Key, instrument.Name) && empties is false)
          {
            var price = await pricesGrain.Store(min.Value.Instrument.Price with
            {
              Bar = null,
              Name = min.Key,
              TimeFrame = instrument.TimeFrame
            });

            await domGrain.Store(min.Value.Dom);
            await optionsGrain.Store(min.Value.Options);
            await dataStream.OnNextAsync(price);

            summaries[instrument.Name] = null;
            pubPrice = price;
          }
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
    public virtual Task<StatusResponse> Unsubscribe(InstrumentState instrument)
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
    protected virtual SummaryState GetSummary(string name, string source)
    {
      var document = new FileInfo(source);

      if (string.Equals(document.Extension, ".bin", StringComparison.InvariantCultureIgnoreCase))
      {
        var content = File.ReadAllBytes(source);

        return MessagePackSerializer.Deserialize<SummaryState>(content, messageOptions);
      }

      if (string.Equals(document.Extension, ".zip", StringComparison.InvariantCultureIgnoreCase))
      {
        using (var stream = File.OpenRead(source))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        using (var content = archive.Entries.First().Open())
        {
          return JsonSerializer.Deserialize<SummaryState>(content, converter.Options);
        }
      }

      var inputMessage = File.ReadAllText(source);

      return JsonSerializer.Deserialize<SummaryState>(inputMessage, converter.Options);
    }
  }
}
