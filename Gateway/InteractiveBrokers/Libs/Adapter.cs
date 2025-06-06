using Distribution.Services;
using IBApi;
using InteractiveBrokers.Enums;
using InteractiveBrokers.Mappers;
using InteractiveBrokers.Messages;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
using Terminal.Core.Services;
using static InteractiveBrokers.IBClient;

namespace InteractiveBrokers
{
  public class Adapter : Gateway
  {
    /// <summary>
    /// Unique order ID
    /// </summary>
    protected int order;

    /// <summary>
    /// Unique request ID
    /// </summary>
    protected int counter;

    /// <summary>
    /// IB client
    /// </summary>
    protected IBClient client;

    /// <summary>
    /// Asset subscriptions
    /// </summary>
    protected ConcurrentDictionary<string, int> subscriptions;

    /// <summary>
    /// Timeout
    /// </summary>
    public virtual TimeSpan Timeout { get; set; }

    /// <summary>
    /// Timeout
    /// </summary>
    public virtual int Interval { get; set; }

    /// <summary>
    /// Host
    /// </summary>
    public virtual string Host { get; set; }

    /// <summary>
    /// Port
    /// </summary>
    public virtual int Port { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      Port = 7497;
      Interval = 100;
      Host = "127.0.0.1";
      Timeout = TimeSpan.FromSeconds(10);

      order = 1;
      counter = 1;
      subscriptions = new ConcurrentDictionary<string, int>();
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<ResponseModel<StatusEnum>> Connect()
    {
      var response = new ResponseModel<StatusEnum>();

      try
      {
        await Disconnect();
        await CreateReader();

        SubscribeToIds();
        SubscribeToErrors();
        SubscribeToOrders();
        SubscribeToStreams();

        await GetAccount();

        foreach (var summary in Account.State.Values)
        {
          await Subscribe(summary.Instrument);
        }

        response.Data = StatusEnum.Active;
      }
      catch (Exception e)
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return response;
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<StatusEnum>> Subscribe(InstrumentModel instrument)
    {
      var response = new ResponseModel<StatusEnum>();

      try
      {
        await Unsubscribe(instrument);

        Account.State[instrument.Name] = Account.State.Get(instrument.Name) ?? new StateModel();
        Account.State[instrument.Name].Instrument ??= instrument;

        var id = subscriptions[instrument.Name] = counter++;
        var contracts = await GetContracts(instrument);
        var contract = contracts.Data.First();

        await SubscribeToPoints(id, InternalMap.GetInstrument(contract, instrument));

        response.Data = StatusEnum.Active;
      }
      catch (Exception e)
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return response;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<ResponseModel<StatusEnum>> Disconnect()
    {
      var response = new ResponseModel<StatusEnum>();

      try
      {
        client?.ClientSocket?.eDisconnect();
        client?.Dispose();

        response.Data = StatusEnum.Active;
      }
      catch (Exception e)
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument)
    {
      var response = new ResponseModel<StatusEnum>();

      try
      {
        if (subscriptions.TryRemove(instrument.Name, out var id))
        {
          client.ClientSocket.cancelMktData(id);
        }

        response.Data = StatusEnum.Active;
      }
      catch (Exception e)
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<InstrumentModel>>> GetOptions(ConditionModel criteria = null)
    {
      var response = new ResponseModel<IList<InstrumentModel>>();

      try
      {
        var id = counter++;
        var instrument = criteria.Instrument;
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contracts = await GetContracts(instrument);

        response.Data = [.. contracts
          .Data
          .Select(o => InternalMap.GetInstrument(o))
          .OrderBy(o => o.Derivative.ExpirationDate)
          .ThenBy(o => o.Derivative.Strike)
          .ThenBy(o => o.Derivative.Side)];
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<DomModel>> GetDom(ConditionModel criteria = null)
    {
      var response = new ResponseModel<DomModel>();

      try
      {
        criteria.MaxDate ??= DateTime.Now;

        var id = counter++;
        var point = (await GetPoints(criteria)).Data.Last();

        response.Data = new DomModel
        {
          Asks = [point],
          Bids = [point]
        };
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<PointModel>>> GetPoints(ConditionModel criteria = null)
    {
      var response = new ResponseModel<IList<PointModel>>();

      try
      {
        var id = counter++;
        var count = criteria.Span ?? 1;
        var instrument = criteria.Instrument;
        var minDate = criteria.MinDate?.ToString($"yyyyMMdd-HH:mm:ss");
        var maxDate = (criteria.MaxDate ?? DateTime.Now).ToString($"yyyyMMdd-HH:mm:ss");
        var contract = ExternalMap.GetContract(instrument);
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void subscribe(HistoricalTicksMessage message)
        {
          if (Equals(id, message.ReqId))
          {
            response.Data = [.. message.Items.Select(o => InternalMap.GetPrice(o, instrument))];
            unsubscribe();
          }
        }

        void unsubscribe()
        {
          client.historicalTicksList -= subscribe;
          source.TrySetResult();
        }

        client.historicalTicksList += subscribe;
        client.ClientSocket.reqHistoricalTicks(id, contract, minDate, maxDate, count, "BID_ASK", 1, false, null);

        await await Task.WhenAny(source.Task, Task.Delay(Timeout).ContinueWith(o => unsubscribe()));
        await Task.Delay(Interval);
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Send orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> SendOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<IList<OrderModel>> { Data = [] };

      foreach (var order in orders)
      {
        try
        {
          var inOrders = ComposeOrders(order);

          foreach (var inOrder in inOrders)
          {
            response.Data.Add((await SendOrder(inOrder)).Data);
          }
        }
        catch (Exception e)
        {
          response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
        }
      }

      await GetAccount();

      return response;
    }

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> ClearOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<IList<OrderModel>>();

      foreach (var order in orders)
      {
        response.Data.Add((await ClearOrder(order)).Data);
      }

      await GetAccount();

      return response;
    }

    /// <summary>
    /// Get contract definition
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<IList<Contract>>> GetContracts(InstrumentModel instrument)
    {
      var id = counter++;
      var response = new ResponseModel<IList<Contract>> { Data = [] };
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      var contract = ExternalMap.GetContract(instrument);

      void subscribe(ContractDetailsMessage message)
      {
        if (Equals(id, message.RequestId))
        {
          response.Data.Add(message.ContractDetails.Contract);
        }
      }

      void unsubscribe(int reqId)
      {
        client.ContractDetails -= subscribe;
        client.ContractDetailsEnd -= unsubscribe;

        source.TrySetResult();
      }

      client.ContractDetails += subscribe;
      client.ContractDetailsEnd += unsubscribe;
      client.ClientSocket.reqContractDetails(id, contract);

      await await Task.WhenAny(source.Task, Task.Delay(Timeout).ContinueWith(o => unsubscribe(id)));
      await Task.Delay(Interval);

      return response;
    }

    /// <summary>
    /// Subscribe to account updates
    /// </summary>
    protected virtual void SubscribeToStreams()
    {
      client.ConnectionClosed += () =>
      {
        var message = new MessageModel<string>
        {
          Message = $"{ClientErrorEnum.NoConnection}",
          Action = ActionEnum.Disconnect
        };

        InstanceService<MessageService>.Instance.OnMessage(message);
      };
    }

    /// <summary>
    /// Subscribe errors
    /// </summary>
    protected virtual void SubscribeToErrors()
    {
      client.Error += async (id, code, message, error, e) =>
      {
        switch (true)
        {
          case true when Equals(code, (int)ClientErrorEnum.NoConnection):
          case true when Equals(code, (int)ClientErrorEnum.ConnectionError): await Connect(); await Task.Delay(1000); break;
        }

        var content = new MessageModel<string>
        {
          Code = code,
          Message = message,
          Error = e
        };

        InstanceService<MessageService>.Instance.OnMessage(content);
      };
    }

    /// <summary>
    /// Subscribe orders
    /// </summary>
    protected virtual void SubscribeToOrders()
    {
      client.OpenOrder += o =>
      {
        OrderStream(new MessageModel<OrderModel> { Next = InternalMap.GetOrder(o) });
      };

      client.ClientSocket.reqAutoOpenOrders(true);
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    public override async Task<ResponseModel<IAccount>> GetAccount()
    {
      var response = new ResponseModel<IAccount>();

      await GetAccountSummary();

      var orders = await GetOrders();
      var positions = await GetPositions();

      Account.Orders = orders.Data.GroupBy(o => o.Id).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
      Account.Positions = positions.Data.GroupBy(o => o.Name).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();

      Account
        .Positions
        .Values
        .ForEach(o =>
        {
          Account.State[o.Name] = Account.State.Get(o.Name) ?? new StateModel();
          Account.State[o.Name].Instrument = o.Transaction.Instrument;
        });

      response.Data = Account;

      return response;
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> GetOrders(ConditionModel criteria = null)
    {
      var response = new ResponseModel<IList<OrderModel>>();
      var orders = new ConcurrentDictionary<string, OrderModel>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(OpenOrderMessage message)
      {
        if (Equals(message.Order.Account, Account.Descriptor))
        {
          orders[$"{message.Order.PermId}"] = InternalMap.GetOrder(message);
        }
      }

      void unsubscribe()
      {
        client.OpenOrder -= subscribe;
        client.OpenOrderEnd -= unsubscribe;

        response.Data = orders?.Values?.ToList() ?? [];
        source.TrySetResult();
      }

      try
      {
        client.OpenOrder += subscribe;
        client.OpenOrderEnd += unsubscribe;
        client.ClientSocket.reqAllOpenOrders();

        await await Task.WhenAny(source.Task, Task.Delay(Timeout).ContinueWith(o => unsubscribe()));
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> GetPositions(ConditionModel criteria = null)
    {
      var id = counter++;
      var positions = new ConcurrentDictionary<string, OrderModel>();
      var response = new ResponseModel<IList<OrderModel>> { Data = [] };
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(PositionMultiMessage message)
      {
        if (Equals(id, message.ReqId) && Equals(message.Account, Account.Descriptor))
        {
          positions[$"{message.Contract.LocalSymbol}"] = InternalMap.GetPosition(message);
        }
      }

      void unsubscribe(int reqId)
      {
        if (Equals(id, reqId))
        {
          client.PositionMulti -= subscribe;
          client.PositionMultiEnd -= unsubscribe;
          client.ClientSocket.cancelPositionsMulti(id);

          response.Data = positions?.Values?.ToList() ?? [];
          source.TrySetResult();
        }
      }

      try
      {
        client.PositionMulti += subscribe;
        client.PositionMultiEnd += unsubscribe;
        client.ClientSocket.reqPositionsMulti(id, Account.Descriptor, string.Empty);

        await await Task.WhenAny(source.Task, Task.Delay(Timeout).ContinueWith(o => unsubscribe(id)));
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="id"></param>
    /// <param name="instrument"></param>
    /// <returns></returns>
    protected virtual async Task SubscribeToPoints(int id, InstrumentModel instrument)
    {
      var max = short.MaxValue;
      var point = new PointModel();
      var contract = ExternalMap.GetContract(instrument);
      var summary = Account.State[instrument.Name];

      double? value(double data, double min, double max, double? original)
      {
        switch (true)
        {
          case true when data < short.MinValue:
          case true when data > short.MaxValue:
          case true when data < min:
          case true when data > max: return original;
        }

        return Math.Round(data, 2);
      }

      void subscribeToComs(TickOptionMessage message)
      {
        if (Equals(id, message.RequestId))
        {
          instrument.Derivative ??= new DerivativeModel();
          instrument.Derivative.Volatility = value(message.ImpliedVolatility, 0, max, instrument.Derivative.Volatility);

          var variance = instrument.Derivative.Variance ??= new VarianceModel();

          variance.Delta = value(message.Delta, -1, 1, variance.Delta);
          variance.Gamma = value(message.Gamma, 0, max, variance.Gamma);
          variance.Theta = value(message.Theta, 0, max, variance.Theta);
          variance.Vega = value(message.Vega, 0, max, variance.Vega);
        }
      }

      void subscribeToPrices(TickPriceMessage message)
      {
        if (Equals(id, message.RequestId))
        {
          switch (ExternalMap.GetEnum<PropertyEnum>(message.Field))
          {
            case PropertyEnum.BidSize: point.BidSize = message.Data ?? point.BidSize; break;
            case PropertyEnum.AskSize: point.AskSize = message.Data ?? point.AskSize; break;
            case PropertyEnum.BidPrice: point.Bid = message.Data ?? point.Bid; break;
            case PropertyEnum.AskPrice: point.Ask = message.Data ?? point.Ask; break;
            case PropertyEnum.LastPrice: point.Last = message.Data ?? point.Last; break;
          }

          point.Last = point.Last is 0 or null ? point.Bid ?? point.Ask : point.Last;
          point.Bid ??= point.Last;
          point.Ask ??= point.Last;

          if (point.Bid is null || point.Ask is null || point.Last is null)
          {
            return;
          }

          point.Time = DateTime.Now;
          point.Instrument = instrument;

          summary.Points.Add(point);
          summary.PointGroups.Add(point, instrument.TimeFrame);
          summary.Instrument = instrument;
          summary.Instrument.Point = summary.PointGroups.Last();

          DataStream(new MessageModel<PointModel> { Next = instrument.Point });
        }
      }

      client.TickPrice += subscribeToPrices;
      client.TickOptionCommunication += subscribeToComs;
      client.ClientSocket.reqMktData(id, contract, string.Empty, false, false, null);

      await Task.Delay(Interval);
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    protected virtual async Task<ResponseModel<IAccount>> GetAccountSummary()
    {
      var id = counter++;
      var response = new ResponseModel<IAccount>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(AccountSummaryMessage message)
      {
        if (Equals(id, message.RequestId) && Equals(message.Tag, AccountSummaryTags.NetLiquidation))
        {
          Account.Balance = double.Parse(message.Value);
        }
      }

      void unsubscribe(AccountSummaryEndMessage message)
      {
        if (Equals(id, message?.RequestId))
        {
          client.AccountSummary -= subscribe;
          client.AccountSummaryEnd -= unsubscribe;
          client.ClientSocket.cancelAccountSummary(id);

          source.TrySetResult();
        }
      }

      client.AccountSummary += subscribe;
      client.AccountSummaryEnd += unsubscribe;
      client.ClientSocket.reqAccountSummary(id, "All", AccountSummaryTags.GetAllTags());

      await await Task.WhenAny(source.Task, Task.Delay(Timeout).ContinueWith(o => unsubscribe(null)));
      await Task.Delay(Interval);

      response.Data = Account;

      return response;
    }

    /// <summary>
    /// Generate next available order ID
    /// </summary>
    /// <returns></returns>
    protected virtual void SubscribeToIds()
    {
      var source = new TaskCompletionSource();

      void subscribe(int o)
      {
        order = o;
        client.NextValidId -= subscribe;
        source.TrySetResult();
      }

      client.NextValidId += subscribe;
      client.ClientSocket.reqIds(-1);
    }

    /// <summary>
    /// Setup socket connection 
    /// </summary>
    /// <returns></returns>
    protected virtual Task<ResponseModel<EReader>> CreateReader()
    {
      var response = new ResponseModel<EReader>();
      var signal = new EReaderMonitorSignal();

      client = new IBClient(signal);
      client.ClientSocket.eConnect(Host, Port, 0);

      var reader = new EReader(client.ClientSocket, signal);
      var process = new Thread(() =>
      {
        while (client.ClientSocket.IsConnected())
        {
          signal.waitForSignal();
          reader.processMsgs();
        }
      });

      process.Start();
      reader.Start();
      response.Data = reader;

      return Task.FromResult(response);
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<OrderModel>> SendOrder(OrderModel order)
    {
      Account.Orders[order.Id] = order;

      await Subscribe(order.Transaction.Instrument);

      var orderId = this.order++;
      var response = new ResponseModel<OrderModel>();
      var exOrders = ExternalMap.GetOrders(orderId, order, Account);
      var exResponses = new Dictionary<int, OpenOrderMessage>();

      foreach (var exOrder in exOrders)
      {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void subscribe(OpenOrderMessage message)
        {
          if (Equals(exOrder.Order.OrderId, message.OrderId))
          {
            exResponses[message.OrderId] = message;
            unsubscribe();
            source.TrySetResult();
          }
        }

        void unsubscribe()
        {
          client.OpenOrder -= subscribe;
          client.OpenOrderEnd -= unsubscribe;
        }

        client.OpenOrder += subscribe;
        client.OpenOrderEnd += unsubscribe;
        client.ClientSocket.placeOrder(exOrder.Order.OrderId, exOrder.Contract, exOrder.Order);

        await await Task.WhenAny(source.Task, Task.Delay(Timeout).ContinueWith(o => unsubscribe()));
        await Task.Delay(Interval);
      }

      response.Data = order;
      response.Data.Transaction.Id = $"{orderId}";
      response.Data.Transaction.Status = InternalMap.GetOrderStatus(exResponses?.Get(orderId)?.OrderState?.Status);

      return response;
    }

    /// <summary>
    /// Cancel order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<OrderModel>> ClearOrder(OrderModel order)
    {
      var response = new ResponseModel<OrderModel>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      var orderId = int.Parse(order.Transaction.Id);
      var exResponse = null as OrderStatusMessage;

      void subscribe(OrderStatusMessage message)
      {
        if (Equals(orderId, message.OrderId))
        {
          exResponse = message;
          unsubscribe();
        }
      }

      void unsubscribe()
      {
        client.OrderStatus -= subscribe;
        source.TrySetResult();
      }

      client.OrderStatus += subscribe;
      client.ClientSocket.cancelOrder(orderId, string.Empty);

      await await Task.WhenAny(source.Task, Task.Delay(Timeout).ContinueWith(o => unsubscribe()));

      response.Data = order;
      response.Data.Transaction.Id = $"{orderId}";
      response.Data.Transaction.Status = InternalMap.GetOrderStatus(exResponse.Status);

      return response;
    }
  }
}
