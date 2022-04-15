using Core.EnumSpace;
using Core.ManagerSpace;
using Core.ModelSpace;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Websocket.Client;

namespace Gateway.Gemini
{
  /// <summary>
  /// Implementation
  /// </summary>
  public class GatewayClient : GatewayModel, IGatewayModel
  {
    /// <summary>
    /// API key
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Secret
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    /// HTTP endpoint
    /// </summary>
    public string Source { get; set; } = "https://api.sandbox.gemini.com";

    /// <summary>
    /// Socket endpoint
    /// </summary>
    public string StreamSource { get; set; } = "wss://api.sandbox.gemini.com";

    /// <summary>
    /// Establish connection with a server
    /// </summary>
    /// <param name="docHeader"></param>
    public override Task Connect()
    {
      return Task.Run(async () =>
      {
        try
        {
          await Disconnect();

          _connections.Add(_serviceClient ??= new ClientService());

          await GetPositions();
          //await GetAccountData();
          //await GetActiveOrders();
          //await GetActivePositions();
          await Subscribe();
        }
        catch (Exception e)
        {
          InstanceManager<LogService>.Instance.Log.Error(e.ToString());
        }
      });
    }

    /// <summary>
    /// Disconnect
    /// </summary>
    /// <returns></returns>
    public override Task Disconnect()
    {
      Unsubscribe();

      _connections.ForEach(o => o.Dispose());
      _connections.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Start streaming
    /// </summary>
    /// <returns></returns>
    public override async Task Subscribe()
    {
      await Unsubscribe();

      // Orders

      var orderSubscription = OrderSenderStream.Subscribe(message =>
      {
        switch (message.Action)
        {
          case ActionEnum.Create: CreateOrders(message.Next); break;
          case ActionEnum.Update: UpdateOrders(message.Next); break;
          case ActionEnum.Delete: DeleteOrders(message.Next); break;
        }
      });

      _subscriptions.Add(orderSubscription);

      // Streaming

      var client = new WebsocketClient(new Uri(StreamSource + "/markets/events"), _streamOptions)
      {
        Name = Account.Name,
        ReconnectTimeout = TimeSpan.FromSeconds(30),
        ErrorReconnectTimeout = TimeSpan.FromSeconds(30)
      };

      var connectionSubscription = client.ReconnectionHappened.Subscribe(message => { });
      var disconnectionSubscription = client.DisconnectionHappened.Subscribe(message => { });
      var messageSubscription = client.MessageReceived.Subscribe(message =>
      {
        dynamic input = JObject.Parse(message.Text);

        var inputStream = $"{ input.type }";

        switch (inputStream)
        {
          case "quote": break;
        }
      });

      _subscriptions.Add(messageSubscription);
      _subscriptions.Add(connectionSubscription);
      _subscriptions.Add(disconnectionSubscription);

      await client.Start();

      var query = new
      {
        linebreak = true,
        symbols = Account.Instruments.Values.Select(o => o.Name)
      };

      client.Send(ConversionManager.Serialize(query));
    }

    public override Task Unsubscribe()
    {
      _subscriptions.ForEach(o => o.Dispose());
      _subscriptions.Clear();

      return Task.FromResult(0);
    }

    public override Task<IEnumerable<ITransactionOrderModel>> CreateOrders(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    public override Task<IEnumerable<ITransactionOrderModel>> UpdateOrders(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    public override Task<IEnumerable<ITransactionOrderModel>> DeleteOrders(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    /// <summary>
    /// Process incoming quotes
    /// </summary>
    /// <param name="input"></param>
    protected void OnInputQuote(dynamic input)
    {
      var dateAsk = ConversionManager.To<long>(input.askdate);
      var dateBid = ConversionManager.To<long>(input.biddate);
      var currentAsk = ConversionManager.To<double>(input.ask);
      var currentBid = ConversionManager.To<double>(input.bid);
      var previousAsk = _point?.Ask ?? currentAsk;
      var previousBid = _point?.Bid ?? currentBid;
      var symbol = $"{ input.symbol }";

      var point = new PointModel
      {
        Ask = currentAsk,
        Bid = currentBid,
        Bar = new PointBarModel(),
        Instrument = Account.Instruments[symbol],
        AskSize = ConversionManager.To<double>(input.asksz),
        BidSize = ConversionManager.To<double>(input.bidsz),
        Time = DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(dateAsk, dateBid)).DateTime,
        Last = ConversionManager.Compare(currentBid, previousBid) ? currentAsk : currentBid
      };

      _point = point;

      UpdatePointProps(point);
    }

    /// <summary>
    /// Process incoming quotes
    /// </summary>
    /// <param name="input"></param>
    protected void OnInputTrade(dynamic input)
    {
    }

    /// <summary>
    /// Get positions
    /// </summary>
    protected async Task<IList<ITransactionPositionModel>> GetPositions()
    {
      var inputs = new
      {
        request = "/v1/mytrades",
        symbol = "btcusd",
        nonce = DateTime.Now.Ticks
      };

      return MapInput.Positions(await Query(inputs));
    }

    /// <summary>
    /// Send query
    /// </summary>
    protected async Task<dynamic> Query(dynamic inputs)
    {
      var query = Convert.ToBase64String(ConversionManager.Bytes(ConversionManager.Serialize(inputs)));
      var queryHeaders = new Dictionary<dynamic, dynamic>()
      {
        ["Cache-Control"] = "no-cache",
        ["Accept"] = "application/json",
        ["X-GEMINI-APIKEY"] = Token,
        ["X-GEMINI-PAYLOAD"] = query,
        ["X-GEMINI-SIGNATURE"] = ConversionManager.Sha384(query, Secret)
      };

      return ConversionManager.Deserialize<dynamic>(await _serviceClient.Post(Source + inputs.request, null, queryHeaders));
    }
  }
}
