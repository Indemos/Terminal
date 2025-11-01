using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using Core.Services;
using MessagePack;
using MessagePack.Resolvers;
using Orleans;
using Simulation.Grains;
using Simulation.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Simulation
{
  public interface IConnectionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Connect(ConnectionModel connection);

    /// <summary>
    /// Disconnect
    /// </summary>
    Task<StatusResponse> Disconnect();

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Subscribe(InstrumentModel instrument);

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Unsubscribe(InstrumentModel instrument);
  }

  /// <summary>
  /// Constructor
  /// </summary>
  /// <param name="messenger"></param>
  public class ConnectionGrain(MessageService messenger) : Grain<ConnectionModel>, IConnectionGrain
  {
    /// <summary>
    /// Messenger
    /// </summary>
    protected MessageService messenger = messenger;

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
    protected ConcurrentDictionary<string, SummaryModel> summaries = new();

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
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Connect(ConnectionModel connection)
    {
      await Disconnect();

      State = connection;

      streams = State.Account.Instruments.ToDictionary(
        o => o.Value.Name,
        o => Directory
          .EnumerateFiles(Path.Combine(State.Source, o.Value.Name), "*", SearchOption.AllDirectories)
          .GetEnumerator())
          .Concurrent();

      await Task.WhenAll(State.Account.Instruments.Values.Select(Subscribe));

      var counter = this.RegisterGrainTimer(
        o => Task.WhenAll(subscriptions.Values.Select(o => o())),
        0,
        TimeSpan.Zero,
        TimeSpan.FromMicroseconds(1)
        );

      connections.AddRange(streams.Values);

      return new StatusResponse
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public virtual Task<StatusResponse> Disconnect()
    {
      connections?.ForEach(o => o.Dispose());
      State?.Account?.Instruments?.Values?.ForEach(o => Unsubscribe(o));

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
    public virtual async Task<StatusResponse> Subscribe(InstrumentModel instrument)
    {
      await Unsubscribe(instrument);

      var descriptor = this.GetPrimaryKeyString();
      var instrumentDescriptor = $"{descriptor}:{instrument.Name}";
      var domGrain = GrainFactory.GetGrain<IDomGrain>(instrumentDescriptor);
      var pricesGrain = GrainFactory.GetGrain<IGatewayPricesGrain>(instrumentDescriptor);
      var optionsGrain = GrainFactory.GetGrain<IGatewayOptionsGrain>(instrumentDescriptor);
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(descriptor);
      var positionsGrain = GrainFactory.GetGrain<IPositionsGrain>(descriptor);

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
          var ordersMap = orders.GroupBy(o => o.Operation.Instrument.Name).ToDictionary(o => o.Key);
          var positionsMap = positions.GroupBy(o => o.Operation.Instrument.Name).ToDictionary(o => o.Key);
          var optionsMap = min.Value.Options.Where(o => ordersMap.ContainsKey(o.Name) || positionsMap.ContainsKey(o.Name));
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
          await messenger.Send(price);

          foreach (var option in optionsMap)
          {
            var optionPrice = option.Price with { Name = option.Name };

            await ordersGrain.Tap(optionPrice);
            await positionsGrain.Tap(optionPrice);
          }

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
    public virtual Task<StatusResponse> Unsubscribe(InstrumentModel instrument)
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
    protected virtual SummaryModel GetSummary(string name, string source)
    {
      var document = new FileInfo(source);

      if (string.Equals(document.Extension, ".bin", StringComparison.InvariantCultureIgnoreCase))
      {
        var content = File.ReadAllBytes(source);

        return MessagePackSerializer.Deserialize<SummaryModel>(content, messageOptions);
      }

      if (string.Equals(document.Extension, ".zip", StringComparison.InvariantCultureIgnoreCase))
      {
        using (var stream = File.OpenRead(source))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        using (var content = archive.Entries.First().Open())
        {
          return JsonSerializer.Deserialize<SummaryModel>(content, converter.Options);
        }
      }

      var inputMessage = File.ReadAllText(source);

      return JsonSerializer.Deserialize<SummaryModel>(inputMessage, converter.Options);
    }
  }
}
