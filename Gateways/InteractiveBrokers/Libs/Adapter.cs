using IBApi;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Models;

namespace InteractiveBrokers
{
  public class Adapter : Gateway
  {
    /// <summary>
    /// IB client
    /// </summary>
    protected IBClient _client;

    /// <summary>
    /// Monitoring signal
    /// </summary>
    private EReaderMonitorSignal _signal;

    /// <summary>
    /// Disposable connections
    /// </summary>
    protected IList<IDisposable> _connections;

    /// <summary>
    /// Disposable subscriptions
    /// </summary>
    protected IList<IDisposable> _subscriptions;

    /// <summary>
    /// Data source
    /// </summary>
    public virtual string Host { get; set; }

    /// <summary>
    /// Streaming source
    /// </summary>
    public virtual string Port { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      Host = "127.0.0.1";
      Port = "7497";

      _subscriptions = [];
      _connections = [];

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
        await GetAccountData();

        Account.Instruments.ForEach(async o => await Subscribe(o.Key));
      }
      catch (Exception e)
      {
        errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return errors;
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    public override async Task<IList<ErrorModel>> Subscribe(string name)
    {
      var errors = new List<ErrorModel>();

      try
      {
        await Unsubscribe(name);
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
      _connections?.ForEach(o => o?.Dispose());
      _connections?.Clear();

      return Task.FromResult<IList<ErrorModel>>([]);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    public override Task<IList<ErrorModel>> Unsubscribe(string name)
    {
      var errors = new List<ErrorModel>();

      try
      {
        _subscriptions?.ForEach(o => o.Dispose());
        _subscriptions?.Clear();
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
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseItemModel<IList<OptionModel>>> GetOptions(Hashtable criteria)
    {
      var response = new ResponseItemModel<IList<OptionModel>>();

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
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseItemModel<IDictionary<string, PointModel>>> GetPoint(Hashtable criteria)
    {
      var response = new ResponseItemModel<IDictionary<string, PointModel>>();

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
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override Task<ResponseItemModel<IList<PointModel>>> GetPoints(Hashtable criteria)
    {
      var response = new ResponseItemModel<IList<PointModel>>();

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
    /// Sync open balance, order, and positions 
    /// </summary>
    /// <returns></returns>
    protected virtual Task GetAccountData()
    {
      //var account = await SendData<AccountMessage>("/v2/account");
      //var positions = await SendData<PositionMessage[]>("/v2/positions");
      //var orders = await SendData<OrderMessage[]>("/v2/orders");

      //Account.Balance = account.Data.Equity;
      //Account.Descriptor = account.Data.AccountNumber;
      //Account.ActiveOrders = orders.Data.Select(InternalMap.GetOrder).ToDictionary(o => o.Transaction.Id, o => o);
      //Account.ActivePositions = positions.Data.Select(InternalMap.GetPosition).ToDictionary(o => o.Order.Transaction.Id, o => o);

      //Account.ActiveOrders.ForEach(async o => await Subscribe(o.Value.Transaction.Instrument.Name));
      //Account.ActivePositions.ForEach(async o => await Subscribe(o.Value.Order.Transaction.Instrument.Name));

      return Task.CompletedTask;
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual Task<ResponseItemModel<OrderModel>> CreateOrder(OrderModel order)
    {
      var inResponse = new ResponseItemModel<OrderModel>();

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
    protected virtual Task<ResponseItemModel<OrderModel>> DeleteOrder(OrderModel order)
    {
      var inResponse = new ResponseItemModel<OrderModel>();

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
