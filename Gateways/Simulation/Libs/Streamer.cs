using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Grains;
using Core.Common.Services;
using Core.Common.Models;
using MessagePack;
using MessagePack.Resolvers;
using Simulation.Grains;
using Simulation.States;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace Simulation
{
  public class Streamer : IDisposable
  {
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
    /// Message pack options
    /// </summary>
    protected MessagePackSerializerOptions messageOptions = MessagePackSerializerOptions
      .Standard
      .WithResolver(ContractlessStandardResolver.Instance);

    /// <summary>
    /// State
    /// </summary>
    public virtual Gateway Adapter { get; set; }

    /// <summary>
    /// Descriptor
    /// </summary>
    public virtual DescriptorModel Descriptor { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    public virtual StatusResponse Connect()
    {
      Disconnect();

      streams = Adapter.Account.Instruments.ToDictionary(
        o => o.Value.Name,
        o => Directory
          .EnumerateFiles(Path.Combine(Adapter.Source, o.Value.Name), "*", SearchOption.AllDirectories)
          .GetEnumerator())
          .Concurrent();

      Adapter.Account.Instruments.Values.ForEach(o => Subscribe(o));

      var scheduler = new SchedulerService();
      var counter = new Timer(TimeSpan.FromMicroseconds(Adapter.Speed));

      counter.Enabled = true;
      counter.AutoReset = false;
      counter.Elapsed += (sender, e) => scheduler.Send(async () =>
      {
        await Task.WhenAll(subscriptions.Values.Select(o => o()));
        counter.Enabled = true;
      });

      connections.AddRange(streams.Values);
      connections.Add(scheduler);
      connections.Add(counter);

      return new StatusResponse
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public virtual StatusResponse Disconnect()
    {
      connections?.ForEach(o => o.Dispose());
      Adapter?.Account?.Instruments?.Values?.ForEach(o => Unsubscribe(o));

      streams?.Clear();
      summaries?.Clear();
      connections?.Clear();
      subscriptions?.Clear();

      return new StatusResponse
      {
        Data = StatusEnum.Inactive
      };
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose() => Disconnect();

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public virtual StatusResponse Subscribe(InstrumentModel instrument)
    {
      Unsubscribe(instrument);

      var instrumentDescriptor = Descriptor with { Instrument = instrument.Name };
      var domGrain = Adapter.Connector.Get<IDomGrain>(instrumentDescriptor);
      var pricesGrain = Adapter.Connector.Get<IGatewayPricesGrain>(instrumentDescriptor);
      var optionsGrain = Adapter.Connector.Get<IOptionsGrain>(instrumentDescriptor);
      var ordersGrain = Adapter.Connector.Get<IOrdersGrain>(Descriptor);
      var positionsGrain = Adapter.Connector.Get<IPositionsGrain>(Descriptor);

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
          var price = await pricesGrain.Store(min.Value.Instrument.Price with
          {
            Bar = null,
            Name = min.Key,
            TimeFrame = instrument.TimeFrame
          });

          await domGrain.Store(min.Value.Dom);
          await optionsGrain.Store(min.Value.Options);
          await ordersGrain.Tap(price);
          await positionsGrain.Tap(price);
          await Adapter.Subscription(price);

          summaries[instrument.Name] = null;
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
    public virtual StatusResponse Unsubscribe(InstrumentModel instrument)
    {
      subscriptions.TryRemove(instrument.Name, out var subscription);

      return new StatusResponse
      {
        Data = StatusEnum.Inactive
      };
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
