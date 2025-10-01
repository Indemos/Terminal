using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using Core.Services;
using IBApi;
using InteractiveBrokers.Enums;
using InteractiveBrokers.Mappers;
using InteractiveBrokers.Messages;
using InteractiveBrokers.Models;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static InteractiveBrokers.IBClient;

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
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> Ticks(MetaModel criteria);

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    Task<AccountModel> AccountSummary();

    /// <summary>
    /// Get contract definition
    /// </summary>
    /// <param name="instrument"></param>
    Task<List<Contract>> Contracts(InstrumentModel instrument);
  }

  public class ConnectionGrain : Grain<ConnectionModel>, IConnectionGrain
  {
    /// <summary>
    /// Next ID
    /// </summary>
    protected int nextId = 0;

    /// <summary>
    /// Next order ID
    /// </summary>
    protected int nextOrderId = 0;

    /// <summary>
    /// IB client
    /// </summary>
    protected IBClient api;

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
    protected ConcurrentDictionary<string, int> subscriptions = new();

    /// <summary>
    /// Data stream
    /// </summary>
    protected IAsyncStream<PriceModel> stream;

    /// <summary>
    /// Order stream
    /// </summary>
    protected IAsyncStream<OrderModel> orderStream;

    /// <summary>
    /// Message stream
    /// </summary>
    protected IAsyncStream<MessageModel> messageStream;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      descriptor = converter.Decompose<DescriptorModel>(this.GetPrimaryKeyString());

      stream = this
        .GetStreamProvider(nameof(StreamEnum.Price))
        .GetStream<PriceModel>(descriptor.Account, Guid.Empty);

      orderStream = this
        .GetStreamProvider(nameof(StreamEnum.Order))
        .GetStream<OrderModel>(descriptor.Account, Guid.Empty);

      messageStream = this
        .GetStreamProvider(nameof(StreamEnum.Message))
        .GetStream<MessageModel>(string.Empty, Guid.Empty);

      await base.OnActivateAsync(cancellation);
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

      Setup();
      SubscribeToIds();
      SubscribeToErrors();
      SubscribeToOrders();
      SubscribeToConnections();

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
      api?.ClientSocket?.eDisconnect();
      api?.Dispose();

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

      var id = subscriptions[instrument.Name] = nextId;
      var contracts = await Contracts(instrument);
      var contract = contracts.First();

      await SubscribeToPrices(id, Downstream.GetInstrument(contract, instrument));

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

      if (subscriptions.TryRemove(instrument.Name, out var id))
      {
        api.ClientSocket.cancelMktData(id);
      }

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Pause
      });
    }

    /// <summary>
    /// Setup socket connection 
    /// </summary>
    public virtual IBClient Setup()
    {
      var signal = new EReaderMonitorSignal();

      api = new IBClient(signal);
      api.ClientSocket.eConnect(State.Host, State.Port, 0);

      var reader = new EReader(api.ClientSocket, signal);
      var process = new Thread(() =>
      {
        while (api.ClientSocket.IsConnected())
        {
          signal.waitForSignal();
          reader.processMsgs();
        }
      });

      process.Start();
      reader.Start();

      return api;
    }

    /// <summary>
    /// Generate next available order ID
    /// </summary>
    protected virtual void SubscribeToIds()
    {
      api.NextValidId += o =>
      {
        nextId = o;
        nextOrderId = o + short.MaxValue;
      };

      api.ClientSocket.reqIds(-1);
    }

    /// <summary>
    /// Subscribe to account updates
    /// </summary>
    protected virtual void SubscribeToConnections()
    {
      api.ConnectionClosed += () => messageStream.OnNextAsync(new()
      {
        Content = $"{ClientErrorEnum.NoConnection}",
        Action = ActionEnum.Disconnect
      });
    }

    /// <summary>
    /// Subscribe errors
    /// </summary>
    protected virtual void SubscribeToErrors()
    {
      api.Error += async (id, code, message, error, e) =>
      {
        switch (true)
        {
          case true when Equals(code, (int)ClientErrorEnum.NoConnection):
          case true when Equals(code, (int)ClientErrorEnum.ConnectionError):
            await Connect();
            await Task.Delay(State.Span);
            break;
        }

        await messageStream.OnNextAsync(new()
        {
          Code = code,
          Content = message,
          Error = e
        });
      };
    }

    /// <summary>
    /// Subscribe orders
    /// </summary>
    protected virtual void SubscribeToOrders()
    {
      api.OpenOrder += o => orderStream.OnNextAsync(Downstream.GetOrder(o));
      api.ClientSocket.reqAutoOpenOrders(true);
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderModel>> Orders(MetaModel criteria)
    {
      var orders = new ConcurrentDictionary<string, OrderModel>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(OpenOrderMessage message)
      {
        if (Equals(message.Order.Account, descriptor.Account))
        {
          orders[$"{message.Order.PermId}"] = Downstream.GetOrder(message);
        }
      }

      void unsubscribe()
      {
        api.OpenOrder -= subscribe;
        api.OpenOrderEnd -= unsubscribe;

        source.TrySetResult();
      }

      api.OpenOrder += subscribe;
      api.OpenOrderEnd += unsubscribe;
      api.ClientSocket.reqAllOpenOrders();

      await await Task.WhenAny(source.Task, Task.Delay(State.Timeout).ContinueWith(o => unsubscribe()));

      return [.. orders.Values];
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderModel>> Positions(MetaModel criteria)
    {
      var id = nextId;
      var positions = new ConcurrentDictionary<string, OrderModel>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(PositionMultiMessage message)
      {
        if (Equals(id, message.ReqId) && Equals(message.Account, State.Account.Name))
        {
          positions[$"{message.Contract.LocalSymbol}"] = Downstream.GetPosition(message);
        }
      }

      void unsubscribe(int reqId)
      {
        if (Equals(id, reqId))
        {
          api.PositionMulti -= subscribe;
          api.PositionMultiEnd -= unsubscribe;
          api.ClientSocket.cancelPositionsMulti(id);

          source.TrySetResult();
        }
      }

      api.PositionMulti += subscribe;
      api.PositionMultiEnd += unsubscribe;
      api.ClientSocket.reqPositionsMulti(id, State.Account.Name, string.Empty);

      await await Task.WhenAny(source.Task, Task.Delay(State.Timeout).ContinueWith(o => unsubscribe(id)));

      return [.. positions.Values];
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<PriceModel>> Ticks(MetaModel criteria)
    {
      var id = nextId;
      var points = new List<PriceModel>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(HistoricalTicksMessage message)
      {
        if (Equals(id, message.ReqId))
        {
          points = [.. message.Items.Select(o => Downstream.GetPrice(o, criteria.Instrument))];
          unsubscribe();
        }
      }

      void unsubscribe()
      {
        api.historicalTicksList -= subscribe;
        source.TrySetResult();
      }

      var count = criteria.Count ?? 1;
      var minDate = criteria.MinDate?.ToString($"yyyyMMdd-HH:mm:ss");
      var maxDate = (criteria.MaxDate ?? DateTime.Now.Ticks).ToString($"yyyyMMdd-HH:mm:ss");
      var contract = Upstream.GetContract(criteria.Instrument);

      api.historicalTicksList += subscribe;
      api.ClientSocket.reqHistoricalTicks(id, contract, minDate, maxDate, count, "BID_ASK", 1, false, null);

      await await Task.WhenAny(source.Task, Task.Delay(State.Timeout).ContinueWith(o => unsubscribe()));
      await Task.Delay(State.Span);

      return points;
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    public virtual async Task<AccountModel> AccountSummary()
    {
      var id = nextId;
      var account = new AccountModel();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(AccountSummaryMessage message)
      {
        if (Equals(id, message.RequestId) && Equals(message.Tag, AccountSummaryTags.NetLiquidation))
        {
          account = account with { Balance = double.Parse(message.Value) };
        }
      }

      void unsubscribe(AccountSummaryEndMessage message)
      {
        if (Equals(id, message?.RequestId))
        {
          api.AccountSummary -= subscribe;
          api.AccountSummaryEnd -= unsubscribe;
          api.ClientSocket.cancelAccountSummary(id);

          source.TrySetResult();
        }
      }

      api.AccountSummary += subscribe;
      api.AccountSummaryEnd += unsubscribe;
      api.ClientSocket.reqAccountSummary(id, "All", AccountSummaryTags.GetAllTags());

      await await Task.WhenAny(source.Task, Task.Delay(State.Timeout).ContinueWith(o => unsubscribe(null)));
      await Task.Delay(State.Span);

      return account;
    }

    /// <summary>
    /// Get contract definition
    /// </summary>
    /// <param name="instrument"></param>
    public virtual async Task<List<Contract>> Contracts(InstrumentModel instrument)
    {
      var id = nextId;
      var response = new List<Contract>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(ContractDetailsMessage message)
      {
        if (Equals(id, message.RequestId))
        {
          response.Add(message.ContractDetails.Contract);
        }
      }

      void unsubscribe(int reqId)
      {
        api.ContractDetails -= subscribe;
        api.ContractDetailsEnd -= unsubscribe;

        source.TrySetResult();
      }

      var contract = Upstream.GetContract(instrument);

      api.ContractDetails += subscribe;
      api.ContractDetailsEnd += unsubscribe;
      api.ClientSocket.reqContractDetails(id, contract);

      await await Task.WhenAny(source.Task, Task.Delay(State.Timeout).ContinueWith(o => unsubscribe(id)));
      await Task.Delay(State.Span);

      return response;
    }

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="id"></param>
    /// <param name="instrument"></param>
    protected virtual async Task SubscribeToPrices(int id, InstrumentModel instrument)
    {
      var max = short.MaxValue;
      var price = new PriceModel();
      var contract = Upstream.GetContract(instrument);
      var instrumentDescriptor = descriptor with { Instrument = instrument.Name };
      var pricesGrain = GrainFactory.Get<IPricesGrain>(instrumentDescriptor);
      var ordersGrain = GrainFactory.Get<IOrdersGrain>(descriptor);
      var positionsGrain = GrainFactory.Get<IPositionsGrain>(descriptor);

      double? value(double data, double min, double max, double? original)
      {
        switch (true)
        {
          case true when data <= short.MinValue:
          case true when data >= short.MaxValue:
          case true when data <= min:
          case true when data >= max: return original;
        }

        return Math.Round(data, 2);
      }

      void subscribeToComs(TickOptionMessage message)
      {
        if (Equals(id, message.RequestId))
        {
          var derivative = instrument.Derivative ?? new();
          var variance = derivative?.Variance ?? new();

          instrument = instrument with
          {
            Derivative = derivative with
            {
              Volatility = value(message.ImpliedVolatility, 0, max, instrument.Derivative.Volatility),
              Variance = variance with
              {
                Delta = value(message.Delta, -1, 1, variance.Delta),
                Gamma = value(message.Gamma, 0, max, variance.Gamma),
                Theta = value(message.Theta, 0, max, variance.Theta),
                Vega = value(message.Vega, 0, max, variance.Vega)
              }
            }
          };
        }
      }

      async Task subscribeToPrices(TickPriceMessage message)
      {
        if (Equals(id, message.RequestId))
        {
          switch (Upstream.GetEnum<PropertyEnum>(message.Field))
          {
            case PropertyEnum.BidSize: price = price with { BidSize = message.Data ?? price.BidSize }; break;
            case PropertyEnum.AskSize: price = price with { AskSize = message.Data ?? price.AskSize }; break;
            case PropertyEnum.BidPrice: price = price with { Bid = message.Data ?? price.Bid }; break;
            case PropertyEnum.AskPrice: price = price with { Ask = message.Data ?? price.Ask }; break;
            case PropertyEnum.LastPrice: price = price with { Last = message.Data ?? price.Last }; break;
          }

          price = price with
          {
            Name = instrument.Name,
            Time = DateTime.Now.Ticks,
            Last = price.Last is 0 or null ? price.Bid ?? price.Ask : price.Last
          };

          if (price.Bid is null || price.Ask is null)
          {
            return;
          }

          await ordersGrain.Tap(price);
          await positionsGrain.Tap(price);
          await pricesGrain.Store(price);
        }
      }

      api.TickPrice += async o => await subscribeToPrices(o);
      api.TickOptionCommunication += subscribeToComs;
      api.ClientSocket.reqMktData(id, contract, string.Empty, false, false, null);

      await Task.Delay(State.Span);
    }
  }
}
