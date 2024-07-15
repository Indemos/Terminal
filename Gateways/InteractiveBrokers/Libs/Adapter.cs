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

        _subscriptions[instrument.Name] = Id;

        _client.ClientSocket.reqTickByTickData(_subscriptions[instrument.Name], contract, "BidAsk", 0, true);
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
    /// <param name="args"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<IList<OptionModel>>> GetOptions(OptionsArgs args, Hashtable criteria)
    {
      var response = new ResponseModel<IList<OptionModel>>();

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
    /// <param name="args"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<DomModel>> GetDom(DomArgs args, Hashtable criteria)
    {
      var response = new ResponseModel<DomModel>();

      try
      {
        var id = Id;
        var source = new TaskCompletionSource<TickByTickBidAskMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var contract = new Contract
        {
          Symbol = $"{criteria["symbol"]}",
          SecType = criteria.Get<string>("secType") ?? "STK",
          Currency = criteria.Get<string>("currency") ?? "USD",
          Exchange = criteria.Get<string>("exchange") ?? "SMART"
        };

        _client.tickByTickBidAsk += quote =>
        {
          source.TrySetResult(quote);
          _client.ClientSocket.cancelTickByTickData(id);
        };

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
    /// <param name="args"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<IList<PointModel>>> GetPoints(PointsArgs args, Hashtable criteria)
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
    /// Limit actions to current account only
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="action"></param>
    protected void SetAccountData(string descriptor, Action action)
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
        await GetAccountUpdates(criteria);

        var orders = await GetOrders(null, criteria);
        var positions = await GetPositions(null, criteria);

        Account.ActiveOrders = orders.Data.ToDictionary(o => o.Transaction.Id);
        Account.ActivePositions = positions.Data.ToDictionary(o => o.Order.Transaction.Id);

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
    /// <param name="args"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> GetOrders(OrdersArgs args, Hashtable criteria)
    {
      var response = new ResponseModel<IList<OrderModel>>();

      try
      {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _client.OpenOrder = o => SetAccountData(o.Order.Account, () => Account.ActiveOrders[$"{o.OrderId}"] = InternalMap.GetOrder(o));
        _client.OpenOrderEnd = () => source.TrySetResult();
        _client.ClientSocket.reqAutoOpenOrders(true);
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
    /// <param name="args"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<PositionModel>>> GetPositions(PositionsArgs args, Hashtable criteria)
    {
      var response = new ResponseModel<IList<PositionModel>>();

      try
      {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _client.Position = o => SetAccountData(o.Account, () => Account.ActivePositions[o.Contract.Symbol] = InternalMap.GetPosition(o));
        _client.PositionEnd = () => source.TrySetResult();
        _client.ClientSocket.reqPositions();

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
    protected virtual async Task<ResponseModel<IAccount>> GetAccountUpdates(Hashtable criteria)
    {
      var response = new ResponseModel<IAccount>();

      try
      {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _client.UpdateAccountValue = o => { };
        _client.AccountSummary = o => SetAccountData(o.Account, () =>
        {
          switch (o.Tag)
          {
            case AccountSummaryTags.NetLiquidation: Account.Balance = double.Parse(o.Value); break;
          }
        });

        _client.AccountSummaryEnd = o => source.TrySetResult();
        _client.ClientSocket.reqAccountSummary(Id, "All", AccountSummaryTags.GetAllTags());
        _client.ClientSocket.reqAccountUpdates(true, Account.Descriptor);

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
    /// Setup socket connection 
    /// </summary>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<EReader>> GetReader()
    {
      var response = new ResponseModel<EReader>();

      try
      {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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

        _client.ConnectionClosed = () => { };
        _client.Error = (id, errorCode, message, errorMessage, e) => { };
        _client.NextValidId = o => source.TrySetResult();

        await source.Task;

        response.Data = reader;
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual Task<ResponseModel<OrderModel>> CreateOrder(OrderModel order)
    {
      var inResponse = new ResponseModel<OrderModel>();

      try
      {
        //var exOrder = ExternalMap.GetOrder(order);
        //var exResponse = await SendData<OrderMessage>("/v2/orders", HttpMethod.Post, exOrder);

        //inResponse.Data = order;
        //inResponse.Data.Transaction.Id = $"{exResponse.Data.OrderId}";
        //inResponse.Data.Transaction.Status = InternalMap.GetStatus(exResponse.Data.OrderStatus);

        //if ((int)exResponse.Message.StatusCode < 400)
        //{
        //  Account.ActiveOrders.Add(order.Transaction.Id, order);
        //}
      }
      catch (Exception e)
      {
        inResponse.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return Task.FromResult(inResponse);
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
