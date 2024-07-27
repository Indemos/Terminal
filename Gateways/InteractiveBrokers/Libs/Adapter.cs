using IBApi;
using InteractiveBrokers.Mappers;
using InteractiveBrokers.Messages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace InteractiveBrokers
{
  public class Adapter : Gateway
  {
    /// <summary>
    /// Unique request ID
    /// </summary>
    protected int _id;

    /// <summary>
    /// IB client
    /// </summary>
    protected IBClient _client;

    /// <summary>
    /// Monitoring signal
    /// </summary>
    public EReaderMonitorSignal _signal;

    /// <summary>
    /// Asset subscriptions
    /// </summary>
    protected Dictionary<string, int> _subscriptions;

    /// <summary>
    /// Data source
    /// </summary>
    public virtual string Host { get; set; }

    /// <summary>
    /// Streaming source
    /// </summary>
    public virtual int Port { get; set; }

    /// <summary>
    /// Unique ID
    /// </summary>
    public virtual int Id => _id++;

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      Host = "127.0.0.1";
      Port = 7497;

      _id = 1;
      _subscriptions = [];
      _signal = new EReaderMonitorSignal();
      _client = new IBClient(_signal);
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<IList<ErrorModel>> Connect()
    {
      var errors = new List<ErrorModel>();

      try
      {
        await Disconnect();
        await GetReader();
        await GetAccount([]);

        SubscribeToOrders();

        Account.Instruments.ForEach(async o => await Subscribe(o.Value));
      }
      catch (Exception e)
      {
        errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return errors;
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<IList<ErrorModel>> Subscribe(InstrumentModel instrument)
    {
      var errors = new List<ErrorModel>();

      try
      {
        await Unsubscribe(instrument);

        var contract = new Contract
        {
          Symbol = instrument.Name,
          SecType = instrument.Security,
          Currency = "USD",
          Exchange = "SMART"
        };

        _client.ClientSocket.reqTickByTickData(_subscriptions[instrument.Name] = Id, contract, "BidAsk", 0, true);
      }
      catch (Exception e)
      {
        errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return errors;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<IList<ErrorModel>> Disconnect()
    {
      _client?.ClientSocket?.eDisconnect();

      return Task.FromResult<IList<ErrorModel>>([]);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override Task<IList<ErrorModel>> Unsubscribe(InstrumentModel instrument)
    {
      var errors = new List<ErrorModel>();

      try
      {
        if (_subscriptions.TryGetValue(instrument.Name, out var id))
        {
          _client?.ClientSocket?.cancelTickByTickData(id);
          _subscriptions.Remove(instrument.Name);
        }
      }
      catch (Exception e)
      {
        errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return Task.FromResult<IList<ErrorModel>>(errors);
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<IList<DerivativeModel>>> GetOptions(OptionScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<DerivativeModel>>();

      try
      {
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<DomModel>> GetDom(DomScreenerModel screener, Hashtable criteria)
    {
      var id = Id;
      var response = new ResponseModel<DomModel>();
      var source = new TaskCompletionSource<TickByTickBidAskMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

      void getDom(TickByTickBidAskMessage o)
      {
        if (Equals(id, o.ReqId))
        {
          source.TrySetResult(o);
          _client.tickByTickBidAsk -= getDom;
        }
      }

      try
      {
        var contract = new Contract
        {
          Symbol = screener.Name ?? $"{criteria["symbol"]}",
          SecType = screener.Security ?? criteria.Get<string>("secType") ?? "STK",
          Currency = screener.Currency ?? criteria.Get<string>("currency") ?? "USD",
          Exchange = screener.Exchange ?? criteria.Get<string>("exchange") ?? "SMART"
        };

        _client.tickByTickBidAsk += getDom;
        _client.ClientSocket.reqTickByTickData(id, contract, "BidAsk", 0, true);

        response.Data = InternalMap.GetDom(await source.Task);
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
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<IList<PointModel>>> GetPoints(PointScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<PointModel>>();

      try
      {
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Send orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseMapModel<OrderModel>> CreateOrders(params OrderModel[] orders)
    {
      var response = new ResponseMapModel<OrderModel>();

      foreach (var order in orders)
      {
        response.Items.Add(await CreateOrder(order));
      }

      return response;
    }

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseMapModel<OrderModel>> DeleteOrders(params OrderModel[] orders)
    {
      var response = new ResponseMapModel<OrderModel>();

      foreach (var order in orders)
      {
        response.Items.Add(await DeleteOrder(order));
      }

      return response;
    }

    /// <summary>
    /// Subscribe to account updates
    /// </summary>
    protected virtual void GetSubscriptions()
    {
      _client.ConnectionClosed += () => { };
      _client.Error += (id, errorCode, message, errorMessage, e) => { };
    }

    /// <summary>
    /// Subscribe orders
    /// </summary>
    protected virtual void SubscribeToOrders()
    {
      _client.OpenOrder += o =>
      {
        var message = new MessageModel<OrderModel>
        {
          Next = InternalMap.GetOrder(o)
        };

        OrderStream(message);
      };

      _client.ClientSocket.reqAutoOpenOrders(true);
    }

    /// <summary>
    /// Limit actions to current account only
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="action"></param>
    protected virtual void ScreenAccount(string descriptor, Action action)
    {
      if (string.Equals(descriptor, Account.Descriptor, StringComparison.InvariantCultureIgnoreCase))
      {
        action();
      }
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IAccount>> GetAccount(Hashtable criteria)
    {
      var response = new ResponseModel<IAccount>();

      try
      {
        await GetAccountSummary(criteria);

        var orders = await GetOrders(null, criteria);
        var positions = await GetPositions(null, criteria);

        Account.ActiveOrders = orders.Data?.ToDictionary(o => o.Transaction.Id) ?? [];
        Account.ActivePositions = positions.Data?.ToDictionary(o => o.Order.Transaction.Id) ?? [];

        Account
          .ActiveOrders
          .Select(o => o.Value.Transaction.Instrument.Name)
          .Concat(Account.ActivePositions.Select(o => o.Value.Order.Transaction.Instrument.Name))
          .Where(o => Account.Instruments.ContainsKey(o) is false)
          .ForEach(o => Account.Instruments.Add(o, new InstrumentModel { Name = o }));

        response.Data = Account;
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> GetOrders(OrderScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<OrderModel>>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(OpenOrderMessage o)
      {
        ScreenAccount(o.Order.Account, () => response.Data.Add(InternalMap.GetOrder(o)));
      }

      void unsubscribe()
      {
        _client.OpenOrder -= subscribe;
        _client.OpenOrderEnd -= unsubscribe;
        source.TrySetResult();
      }

      try
      {
        _client.OpenOrder += subscribe;
        _client.OpenOrderEnd += unsubscribe;
        _client.ClientSocket.reqAllOpenOrders();

        await source.Task;
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
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<PositionModel>>> GetPositions(PositionScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<PositionModel>>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      var id = Id;

      void subscribe(PositionMultiMessage o)
      {
        if (Equals(id, o.ReqId))
        {
          ScreenAccount(o.Account, () => response.Data.Add(InternalMap.GetPosition(o)));
        }
      }

      void unsubscribe(int reqId)
      {
        if (Equals(id, reqId))
        {
          _client.PositionMulti -= subscribe;
          _client.PositionMultiEnd -= unsubscribe;
          source.TrySetResult();
        }
      }

      try
      {
        _client.PositionMulti += subscribe;
        _client.PositionMultiEnd += unsubscribe;
        _client.ClientSocket.reqPositionsMulti(id, Account.Descriptor, string.Empty);

        await source.Task;
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<IAccount>> GetAccountSummary(Hashtable criteria)
    {
      var response = new ResponseModel<IAccount>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      var id = Id;

      void subscribe(AccountSummaryMessage o) => ScreenAccount(o.Account, () =>
      {
        if (Equals(id, o.RequestId))
        {
          switch (o.Tag)
          {
            case AccountSummaryTags.NetLiquidation: Account.Balance = double.Parse(o.Value); break;
          }
        }
      });

      void unsubscribe(AccountSummaryEndMessage o)
      {
        if (Equals(id, o.RequestId))
        {
          _client.AccountSummary -= subscribe;
          _client.AccountSummaryEnd -= unsubscribe;
          source.TrySetResult();
        }
      }

      try
      {
        _client.AccountSummary += subscribe;
        _client.AccountSummaryEnd += unsubscribe;
        _client.ClientSocket.reqAccountSummary(id, "All", AccountSummaryTags.GetAllTags());

        await source.Task;

        response.Data = Account;
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Generate next available order ID
    /// </summary>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<int>> GetOrderId()
    {
      var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
      var response = new ResponseModel<int>();

      _client.NextValidId += o => source.TrySetResult(o);
      _client.ClientSocket.reqIds(-1);

      response.Data = await source.Task;

      return response;
    }

    /// <summary>
    /// Setup socket connection 
    /// </summary>
    /// <returns></returns>
    protected virtual Task<ResponseModel<EReader>> GetReader()
    {
      var response = new ResponseModel<EReader>();

      try
      {
        _client.ClientSocket.SetConnectOptions("+PACEAPI");
        _client.ClientSocket.eConnect(Host, Port, 0);

        var reader = new EReader(_client.ClientSocket, _signal);
        var processor = new Thread(() =>
        {
          while (_client.ClientSocket.IsConnected())
          {
            _signal.waitForSignal();
            reader.processMsgs();
          }
        })
        { IsBackground = true };

        reader.Start();
        processor.Start();

        response.Data = reader;
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<OrderModel>> CreateOrder(OrderModel order)
    {
      var inResponse = new ResponseModel<OrderModel>();
      var source = new TaskCompletionSource<OpenOrderMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

      try
      {
        var exOrder = ExternalMap.GetOrder(order);
        var orderId = _client.NextOrderId++;

        void createOrder(OpenOrderMessage o)
        {
          if (Equals(orderId, o.OrderId))
          {
            source.TrySetResult(o);
            _client.OpenOrder -= createOrder;
          }
        }

        _client.OpenOrder += createOrder;
        _client.ClientSocket.placeOrder(orderId, exOrder.Contract, exOrder.Order);

        var exResponse = await source.Task;

        inResponse.Data = order;
        inResponse.Data.Transaction.Id = $"{orderId}";
        inResponse.Data.Transaction.Status = InternalMap.GetStatus(exResponse.OrderState.Status);

        if (string.Equals(exResponse.OrderState.Status, "Submitted", StringComparison.InvariantCultureIgnoreCase))
        {
          Account.ActiveOrders.Add(order.Transaction.Id, order);
        }
      }
      catch (Exception e)
      {
        inResponse.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return inResponse;
    }

    /// <summary>
    /// Cancel order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual Task<ResponseModel<OrderModel>> DeleteOrder(OrderModel order)
    {
      var inResponse = new ResponseModel<OrderModel>();

      try
      {
        //var exResponse = await SendData<OrderMessage>($"/v2/orders/{order.Transaction.Id}", HttpMethod.Delete);

        //inResponse.Data = order;
        //inResponse.Data.Transaction.Id = $"{exResponse.Data.OrderId}";
        //inResponse.Data.Transaction.Status = InternalMap.GetStatus(exResponse.Data.OrderStatus);

        //if ((int)exResponse.Message.StatusCode < 400)
        //{
        //  Account.ActiveOrders.Remove(order.Transaction.Id);
        //}
      }
      catch (Exception e)
      {
        inResponse.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return Task.FromResult(inResponse);
    }
  }
}
