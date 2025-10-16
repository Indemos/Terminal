using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using Core.Services;
using IBKRWrapper;
using InteractiveBrokers.Mappers;
using InteractiveBrokers.Models;
using Orleans;
using SimpleBroker;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveBrokers
{
  public interface IConnectionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Store(ConnectionModel connection);

    /// <summary>
    /// Connect
    /// </summary>
    Task<StatusResponse> Connect();

    /// <summary>
    /// Save state and dispose
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

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> Bars(MetaModel criteria);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> Ticks(MetaModel criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Orders(MetaModel criteria);

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Positions(MetaModel criteria);

    /// <summary>
    /// Get contract definitions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<InstrumentModel>> Options(MetaModel criteria);

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    Task<AccountModel> AccountSummary();
  }

  public class ConnectionGrain : Grain<ConnectionModel>, IConnectionGrain
  {
    /// <summary>
    /// IB socket client
    /// </summary>
    protected Wrapper wrapper;

    /// <summary>
    /// IB client
    /// </summary>
    protected Broker connector;

    /// <summary>
    /// Stream
    /// </summary>
    protected StreamService streamer;

    /// <summary>
    /// Descriptor
    /// </summary>
    protected DescriptorModel descriptor;

    /// <summary>
    /// Converter
    /// </summary>
    protected ConversionService converter = new();

    /// <summary>
    /// Asset subscriptions
    /// </summary>
    protected ConcurrentDictionary<string, IDisposable> subscriptions = new();

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="streamService"></param>
    public ConnectionGrain(StreamService streamService) => streamer = streamService;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cleaner"></param>
    public override async Task OnActivateAsync(CancellationToken cleaner)
    {
      descriptor = converter.Decompose<DescriptorModel>(this.GetPrimaryKeyString());
      await base.OnActivateAsync(cleaner);
    }

    /// <summary>
    /// Deactivation
    /// </summary>
    /// <param name="reason"></param>
    /// <param name="cleaner"></param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cleaner)
    {
      await Disconnect();
      await base.OnActivateAsync(cleaner);
    }

    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    public virtual Task<StatusResponse> Store(ConnectionModel connection)
    {
      State = connection;

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }

    /// <summary>
    /// Connect
    /// </summary>
    public virtual async Task<StatusResponse> Connect()
    {
      await Disconnect();

      connector = new Broker();
      connector.Connect(State.Host, State.Port, 0);
      wrapper = (Wrapper)connector
        .GetType()
        .GetField("_wrapper", BindingFlags.NonPublic | BindingFlags.Instance)?
        .GetValue(connector);

      foreach (var instrument in State.Account.Instruments.Values)
      {
        await Subscribe(instrument);
      }

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public virtual Task<StatusResponse> Disconnect()
    {
      connector?.Disconnect();

      return Task.FromResult(new StatusResponse()
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

      var contracts = await Contracts(instrument);
      var contract = contracts.FirstOrDefault();

      if (contract is null)
      {
        await streamer.Send(new MessageModel()
        {
          Content = "No such instrument",
          Action = ActionEnum.Disconnect
        });

        return new()
        {
          Data = StatusEnum.Inactive
        };
      }

      var max = short.MaxValue;
      var price = new PriceModel();
      var id = wrapper.NextOrderId;
      var instrumentDescriptor = descriptor with { Instrument = instrument.Name };
      var pricesGrain = GrainFactory.Get<IPricesGrain>(instrumentDescriptor);
      var ordersGrain = GrainFactory.Get<IOrdersGrain>(descriptor);
      var positionsGrain = GrainFactory.Get<IPositionsGrain>(descriptor);

      //void subscribeToComs(object e, MarketDataEventArgs<IBKRWrapper.Models.OptionGreeks> message)
      //{
      //  if (Equals(id, message.Data.ReqId) && instrument.Type is InstrumentEnum.Options)
      //  {
      //    instrument.Derivative ??= new DerivativeModel();
      //    instrument.Derivative.Volatility = value(message.ImpliedVolatility, 0, max, instrument.Derivative.Volatility);

      //    var variance = instrument.Derivative.Variance ??= new VarianceModel();

      //    variance.Delta = value(message.Delta, -1, 1, variance.Delta);
      //    variance.Gamma = value(message.Gamma, 0, max, variance.Gamma);
      //    variance.Theta = value(message.Theta, 0, max, variance.Theta);
      //    variance.Vega = value(message.Vega, 0, max, variance.Vega);
      //  }
      //}

      //void subscribeToPrices(object e, TickBidAskEventArgs message)
      //{
      //  if (Equals(id, message.ReqId))
      //  {
      //    price = price with
      //    {
      //      Time = DateTime.Now.Ticks,
      //    };

      //    //summary.Points.Add(point);
      //    //summary.PointGroups.Add(point, summary.TimeFrame);
      //    //summary.Instrument = instrument;
      //    //summary.Instrument.Point = summary.PointGroups.Last();
      //    //summary.Instrument.Point.Instrument = instrument;
      //  }
      //}

      //wrapper.TickBidAskEvent += subscribeToPrices;
      //wrapper.OptionGreeksMarketDataEvent += subscribeToComs;
      //wrapper.ClientSocket.reqMktData(id, contract, string.Empty, false, false, null);

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
      if (subscriptions.TryRemove(instrument.Name, out var subscription))
      {
        subscription.Dispose();
      }

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Pause
      });
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderModel>> Orders(MetaModel criteria)
    {
      var sourceItems = await connector.GetOpenOrders();
      var items = sourceItems.Select(Downstream.Order).ToArray();

      await Task.Delay(State.Span);

      return items;
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderModel>> Positions(MetaModel criteria)
    {
      var sourceItems = await connector.GetPositions();
      var items = sourceItems.Select(o => Downstream.Position(o, criteria.Instrument)).ToArray();

      await Task.Delay(State.Span);

      return items;
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<PriceModel>> Ticks(MetaModel criteria)
    {
      var contract = Upstream.Contract(criteria.Instrument);
      var minDate = criteria.MinDate?.ToString($"yyyyMMdd-HH:mm:ss");
      var maxDate = (criteria.MaxDate ?? DateTime.Now.Ticks).ToString($"yyyyMMdd-HH:mm:ss");
      var sourceItems = await connector.GetHistoricalTicksBidAsk(contract, minDate, maxDate, criteria.Count ?? 1, false, true);
      var items = sourceItems.Select(o => Downstream.Price(o, criteria.Instrument)).ToArray();

      await Task.Delay(State.Span);

      return items;
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<PriceModel>> Bars(MetaModel criteria)
    {
      var contract = Upstream.Contract(criteria.Instrument);
      var maxDate = (criteria.MaxDate ?? DateTime.Now.Ticks).ToString($"yyyyMMdd-HH:mm:ss");
      var sourceItems = await connector.GetHistoricalBars(
        contract,
        criteria.Data.Get("Duration"),
        criteria.Data.Get("BarSize"),
        criteria.Data.Get("BarType"),
        false,
        maxDate);

      var items = sourceItems.Select(Downstream.Price).ToArray();

      await Task.Delay(State.Span);

      return items;
    }

    /// <summary>
    /// List options
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<InstrumentModel>> Options(MetaModel criteria)
    {
      var instrument = criteria.Instrument;
      var minDate = criteria.MinDate?.ToString($"yyyyMMdd-HH:mm:ss");
      var maxDate = (criteria.MaxDate ?? DateTime.Now.Ticks).ToString($"yyyyMMdd-HH:mm:ss");
      var contract = Upstream.Contract(criteria.Instrument);
      var sourceItems = await connector.GetFullyDefinedOptionContractsByDate(
        instrument.Name,
        Upstream.InstrumentType(instrument.Type),
        instrument.Exchange,
        instrument.Currency.Name);

      var items = sourceItems.Select(Downstream.Instrument).ToArray();

      await Task.Delay(State.Span);

      return items;
    }

    /// <summary>
    /// Get contract definition
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    protected virtual async Task<List<Contract>> Contracts(InstrumentModel instrument)
    {
      var items = await connector.GetFullyDefinedContracts(
        instrument?.Basis?.Name ?? instrument.Name,
        Upstream.InstrumentType(instrument.Type),
        instrument.Exchange,
        instrument.Currency.Name);

      if (instrument.Type is InstrumentEnum.Futures)
      {
        var expDate = instrument?.Derivative?.TradeDate ?? instrument?.Derivative?.ExpirationDate;

        items = [.. items.Where(o =>
          o.LocalSymbol.Equals(instrument.Name) ||
          o.LastTradeDateOrContractMonth.Equals($"{expDate:yyyyMMdd}"))];
      }

      return items;
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    public virtual async Task<AccountModel> AccountSummary()
    {
      var account = new AccountModel();
      var message = await connector.GetAccountValues(State.Account.Name);

      account = account with { Balance = double.Parse(message.Get("NetLiquidation")) };

      await Task.Delay(State.Span);

      return account;
    }
  }
}
