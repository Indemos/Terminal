using IBApi;
using InteractiveBrokers.Enums;
using InteractiveBrokers.Mappers;
using InteractiveBrokers.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
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
      return await Response(async () =>
      {
        await Disconnect();
        await CreateReader();

        SubscribeToIds();
        SubscribeToErrors();
        SubscribeToOrders();
        SubscribeToStreams();

        await GetAccount();

        foreach (var summary in Account.States.Values)
        {
          await Subscribe(summary.Instrument);
        }

        return StatusEnum.Active;
      });
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<StatusEnum>> Subscribe(InstrumentModel instrument)
    {
      return await Response(async () =>
      {
        await Unsubscribe(instrument);

        Account.States.Get(instrument.Name).Instrument ??= instrument;

        var id = subscriptions[instrument.Name] = counter++;
        var contracts = await GetContracts(instrument);
        var contract = contracts.Data.First();

        await SubscribeToPoints(id, Downstream.GetInstrument(contract, instrument));

        return StatusEnum.Active;
      });
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<ResponseModel<StatusEnum>> Disconnect()
    {
      return Response(() =>
      {
        client?.ClientSocket?.eDisconnect();
        client?.Dispose();

        return Task.FromResult(StatusEnum.Active);
      });
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument)
    {
      return Response(() =>
      {
        if (subscriptions.TryRemove(instrument.Name, out var id))
        {
          client.ClientSocket.cancelMktData(id);
        }

        return Task.FromResult(StatusEnum.Active);
      });
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<InstrumentModel>>> GetOptions(ConditionModel criteria = null)
    {
      return await Response(async () =>
      {
        var id = counter++;
        var instrument = criteria.Instrument;
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contracts = await GetContracts(instrument);

        return contracts
          .Data
          .Select(o => Downstream.GetInstrument(o))
          .OrderBy(o => o.Derivative.ExpirationDate)
          .ThenBy(o => o.Derivative.Strike)
          .ThenBy(o => o.Derivative.Side)
          .ToList();
      });
    }

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<DomModel>> GetDom(ConditionModel criteria = null)
    {
      return await Response(async () =>
      {
        criteria.MaxDate ??= DateTime.Now;

        var id = counter++;
        var point = (await GetPoints(criteria)).Data.Last();

        return new DomModel
        {
          Asks = [point],
          Bids = [point],
        };
      });
    }

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<PointModel>>> GetPoints(ConditionModel criteria = null)
    {
      var id = counter++;
      var points = new List<PointModel>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(HistoricalTicksMessage message) => Observe(() =>
      {
        if (Equals(id, message.ReqId))
        {
          points = [.. message.Items.Select(o => Downstream.GetPrice(o, criteria.Instrument))];
          unsubscribe();
        }
      });

      void unsubscribe() => Observe(() =>
      {
        client.historicalTicksList -= subscribe;
        source.TrySetResult();
      });

      return await Response(async () =>
      {
        var count = criteria.Span ?? 1;
        var minDate = criteria.MinDate?.ToString($"yyyyMMdd-HH:mm:ss");
        var maxDate = (criteria.MaxDate ?? DateTime.Now).ToString($"yyyyMMdd-HH:mm:ss");
        var contract = Upstream.GetContract(criteria.Instrument);

        client.historicalTicksList += subscribe;
        client.ClientSocket.reqHistoricalTicks(id, contract, minDate, maxDate, count, "BID_ASK", 1, false, null);

        await await Task.WhenAny(source.Task, Task.Delay(Timeout).ContinueWith(o => unsubscribe()));
        await Task.Delay(Interval);

        return points;
      });
    }

    /// <summary>
    /// Send orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<OrderModel>> SendOrder(OrderModel order)
    {
      var response = new ResponseModel<OrderModel>();

      if ((response.Errors = await SubscribeToOrder(order)).Count is 0)
      {
        Account.Orders[order.Id] = order;

        var orderId = this.order++;
        var exOrders = Upstream.GetOrders(orderId, order, Account);
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
        response.Data.Id = $"{orderId}";
        response.Data.Status = Downstream.GetOrderStatus(exResponses?.Get(orderId)?.OrderState?.Status);
      }

      return response;
    }

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<OrderModel>>> ClearOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<List<OrderModel>>();

      foreach (var order in orders)
      {
        var o = await Response(async () => await ClearOrder(order));

        response.Errors = [.. response.Errors.Concat(o.Errors)];
        response.Data = [.. response.Data.Append(order)];
      }

      response.Errors = [.. response.Errors.Concat((await GetAccount()).Errors)];

      return response;
    }

    /// <summary>
    /// Get contract definition
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<List<Contract>>> GetContracts(InstrumentModel instrument)
    {
      var id = counter++;
      var response = new List<Contract>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(ContractDetailsMessage message) => Observe(() =>
      {
        if (Equals(id, message.RequestId))
        {
          response.Add(message.ContractDetails.Contract);
        }
      });

      void unsubscribe(int reqId) => Observe(() =>
      {
        client.ContractDetails -= subscribe;
        client.ContractDetailsEnd -= unsubscribe;

        source.TrySetResult();
      });

      return await Response(async () =>
      {
        var contract = Upstream.GetContract(instrument);

        client.ContractDetails += subscribe;
        client.ContractDetailsEnd += unsubscribe;
        client.ClientSocket.reqContractDetails(id, contract);

        await await Task.WhenAny(source.Task, Task.Delay(Timeout).ContinueWith(o => unsubscribe(id)));
        await Task.Delay(Interval);

        return response;
      });
    }

    /// <summary>
    /// Subscribe to account updates
    /// </summary>
    protected virtual void SubscribeToStreams()
    {
      client.ConnectionClosed += () => Observe(() =>
      {
        var message = new MessageModel<string>
        {
          Content = $"{ClientErrorEnum.NoConnection}",
          Action = ActionEnum.Disconnect
        };

        Message(message);
      });
    }

    /// <summary>
    /// Subscribe errors
    /// </summary>
    protected virtual void SubscribeToErrors()
    {
      client.Error += async (id, code, message, error, e) => await Observe(async () =>
      {
        switch (true)
        {
          case true when Equals(code, (int)ClientErrorEnum.NoConnection):
          case true when Equals(code, (int)ClientErrorEnum.ConnectionError): await Connect(); await Task.Delay(1000); break;
        }

        var content = new MessageModel<string>
        {
          Code = code,
          Content = message,
          Error = e
        };

        Message(content);
      });
    }

    /// <summary>
    /// Subscribe orders
    /// </summary>
    protected virtual void SubscribeToOrders()
    {
      client.OpenOrder += o => Observe(() =>
      {
        OrderStream(new MessageModel<OrderModel> { Next = Downstream.GetOrder(o) });
      });

      client.ClientSocket.reqAutoOpenOrders(true);
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    public override async Task<ResponseModel<IAccount>> GetAccount()
    {
      return await Response(async () =>
      {
        await GetAccountSummary();

        var orders = await GetOrders();
        var positions = await GetPositions();

        Account.Orders = orders.Data.GroupBy(o => o.Id).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
        Account.Positions = positions.Data.GroupBy(o => o.Name).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
        Account.Positions.Values.ForEach(async o => await Subscribe(o.Instrument));

        return Account;
      });
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<OrderModel>>> GetOrders(ConditionModel criteria = null)
    {
      var orders = new ConcurrentDictionary<string, OrderModel>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(OpenOrderMessage message)
      {
        if (Equals(message.Order.Account, Account.Descriptor))
        {
          orders[$"{message.Order.PermId}"] = Downstream.GetOrder(message);
        }
      }

      void unsubscribe()
      {
        client.OpenOrder -= subscribe;
        client.OpenOrderEnd -= unsubscribe;

        source.TrySetResult();
      }

      return await Response(async () =>
      {
        client.OpenOrder += subscribe;
        client.OpenOrderEnd += unsubscribe;
        client.ClientSocket.reqAllOpenOrders();

        await await Task.WhenAny(source.Task, Task.Delay(Timeout).ContinueWith(o => unsubscribe()));

        return orders.Values.ToList();
      });
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<OrderModel>>> GetPositions(ConditionModel criteria = null) => await Response(async () =>
    {
      var id = counter++;
      var positions = new ConcurrentDictionary<string, OrderModel>();
      var response = new ResponseModel<List<OrderModel>> { Data = [] };
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      void subscribe(PositionMultiMessage message)
      {
        if (Equals(id, message.ReqId) && Equals(message.Account, Account.Descriptor))
        {
          positions[$"{message.Contract.LocalSymbol}"] = Downstream.GetPosition(message);
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

      client.PositionMulti += subscribe;
      client.PositionMultiEnd += unsubscribe;
      client.ClientSocket.reqPositionsMulti(id, Account.Descriptor, string.Empty);

      await await Task.WhenAny(source.Task, Task.Delay(Timeout).ContinueWith(o => unsubscribe(id)));

      return positions.Values.ToList();
    });

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
      var contract = Upstream.GetContract(instrument);
      var summary = Account.States.Get(instrument.Name);

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

      void subscribeToComs(TickOptionMessage message) => Observe(() =>
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
      });

      void subscribeToPrices(TickPriceMessage message) => Observe(() =>
      {
        if (Equals(id, message.RequestId))
        {
          switch (Upstream.GetEnum<PropertyEnum>(message.Field))
          {
            case PropertyEnum.BidSize: point.BidSize = message.Data ?? point.BidSize; break;
            case PropertyEnum.AskSize: point.AskSize = message.Data ?? point.AskSize; break;
            case PropertyEnum.BidPrice: point.Bid = message.Data ?? point.Bid; break;
            case PropertyEnum.AskPrice: point.Ask = message.Data ?? point.Ask; break;
            case PropertyEnum.LastPrice: point.Last = message.Data ?? point.Last; break;
          }

          point.Last = point.Last is 0 or null ? point.Bid ?? point.Ask : point.Last;

          if (point.Bid is null || point.Ask is null)
          {
            return;
          }

          point.Account = Account;
          point.Time = DateTime.Now;
          point.Name = instrument.Name;
          point.TimeFrame = summary.TimeFrame;

          summary.Points.Add(point);
          summary.PointGroups.Add(point, summary.TimeFrame);
          summary.Instrument = instrument;
          summary.Instrument.Point = summary.PointGroups.Last();

          Stream(new MessageModel<PointModel> { Next = instrument.Point });
        }
      });

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
    /// Cancel order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<OrderModel>> ClearOrder(OrderModel order)
    {
      var response = new ResponseModel<OrderModel>();
      var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      var orderId = int.Parse(order.Id);
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
      response.Data.Id = $"{orderId}";
      response.Data.Status = Downstream.GetOrderStatus(exResponse.Status);

      return response;
    }
  }
}
