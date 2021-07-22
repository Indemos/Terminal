using Core.EnumSpace;
using Core.ManagerSpace;
using Core.ModelSpace;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Tradier.Client;
using Websocket.Client;

namespace Gateway.Tradier
{
  /// <summary>
  /// Implementation
  /// </summary>
  public class GatewayClient : GatewayModel, IGatewayModel
  {
    /// <summary>
    /// API client
    /// </summary>
    protected TradierClient _webClient = null;

    /// <summary>
    /// HTTP client
    /// </summary>
    protected IRemoteService _serviceClient = null;

    /// <summary>
    /// Instance of the streaming client
    /// </summary>
    protected IWebsocketClient _webSocketClient = null;

    /// <summary>
    /// Socket session ID
    /// </summary>
    protected string _webSocketSession = null;

    /// <summary>
    /// Socket connection options
    /// </summary>
    protected Func<ClientWebSocket> _webSocketOptions = new Func<ClientWebSocket>(() =>
    {
      return new ClientWebSocket
      {
        Options =
        {
          KeepAliveInterval = TimeSpan.FromSeconds(30)
        }
      };
    });

    /// <summary>
    /// API key
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// HTTP endpoint
    /// </summary>
    public string Source { get; set; } = "https://api.tradier.com/v1";

    /// <summary>
    /// Socket endpoint
    /// </summary>
    public string StreamSource { get; set; } = "wss://ws.tradier.com/v1";

    /// <summary>
    /// Account name
    /// </summary>
    public string SandboxName { get; set; }

    /// <summary>
    /// API key
    /// </summary>
    public string SandboxToken { get; set; }

    /// <summary>
    /// Sandbox HTTP endpoint
    /// </summary>
    public string SandboxSource { get; set; } = "https://sandbox.tradier.com/v1";

    /// <summary>
    /// Production or Sandbox
    /// </summary>
    public EnvironmentEnum Mode { get; set; } = EnvironmentEnum.Development;

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

          OrderSenderStream
            .TakeUntil(_subscriptions)
            .Subscribe(message =>
            {
              switch (message.Action)
              {
                case ActionEnum.Create: CreateOrder(message.Next); break;
                case ActionEnum.Update: UpdateOrder(message.Next); break;
                case ActionEnum.Delete: DeleteOrder(message.Next); break;
              }
            });

          switch (Mode)
          {
            case EnvironmentEnum.Production: _webClient = new TradierClient(Token, Name, useProduction: true); break;
            case EnvironmentEnum.Development: _webClient = new TradierClient(SandboxToken, SandboxName, useProduction: false); break;
          }

          _webSocketClient = await GetWebSocketClient();

          var balance = await _webClient.Account.GetBalances();
          var orders = await _webClient.Account.GetOrders();
          var positions = await _webClient.Account.GetPositions();
          var history = await _webClient.Account.GetHistory();
          var gainLoss = await _webClient.Account.GetGainLoss();

          var query = new
          {
            linebreak = true,
            sessionid = _webSocketSession,
            symbols = Account.Instruments.Values.Select(o => o.Name)
          };

          _webSocketClient.Send(ConversionManager.Serialize(query));
        }
        catch (Exception e)
        {
          InstanceManager<LogService>.Instance.Log.Error(e.ToString());
        }
      });
    }

    public override Task Disconnect()
    {
      return Task.Run(() =>
      {
        _webSocketClient?.Dispose();
        _serviceClient?.Client?.Dispose();
      });
    }

    public override Task Subscribe()
    {
      return Task.Run(() =>
      {
        _subscriptions.OnNext(true);
      });
    }

    public override Task Unsubscribe()
    {
      return Task.Run(() =>
      {
        _subscriptions.OnNext(false);
      });
    }

    public virtual Task<IEnumerable<ITransactionOrderModel>> CreateOrder(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    public virtual Task<IEnumerable<ITransactionOrderModel>> UpdateOrder(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    public virtual Task<IEnumerable<ITransactionOrderModel>> DeleteOrder(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    /// <summary>
    /// Get streaming client 
    /// </summary>
    /// <returns></returns>
    protected async Task<IWebsocketClient> GetWebSocketClient()
    {
      _serviceClient = new RemoteService();
      _serviceClient.Client.DefaultRequestHeaders.Add("Accept", "application/json");
      _serviceClient.Client.DefaultRequestHeaders.Add("Authorization", "Bearer " + Token);

      var session = await _serviceClient.Post<JObject>(Source + "/markets/events/session");

      _webSocketSession = session["stream"]["sessionid"].ToString();

      var client = new WebsocketClient(new Uri(StreamSource + "/markets/events"), _webSocketOptions)
      {
        Name = Account.Name,
        ReconnectTimeout = TimeSpan.FromSeconds(30),
        ErrorReconnectTimeout = TimeSpan.FromSeconds(30)
      };

      client.ReconnectionHappened.TakeUntil(_subscriptions).Subscribe(message => { });
      client.DisconnectionHappened.TakeUntil(_subscriptions).Subscribe(message => { });
      client.MessageReceived.TakeUntil(_subscriptions).Subscribe(message =>
      {
        var messageModel = JObject.Parse(message.Text);
        var messageType = $"{ messageModel["type"] }";

        switch (messageType)
        {
          case "quote":

            var pointModel = CreatePoint(messageModel);
            var instrumentModel = Account.Instruments[pointModel.Instrument.Name];

            instrumentModel.Points.Add(pointModel);
            instrumentModel.PointGroups.Add(pointModel, instrumentModel.TimeFrame);

            break;

          case "trade": break;
          case "tradex": break;
          case "summary": break;
          case "timesale": break;
        }
      });

      await client.Start();

      return client;
    }

    /// <summary>
    /// Parse JSON into a data point
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    protected IPointModel CreatePoint(JObject input)
    {
      var dateAsk = ConversionManager.Value<long>(input["askdate"]);
      var dateBid = ConversionManager.Value<long>(input["biddate"]);

      return new PointModel
      {
        Ask = ConversionManager.Value<double>(input["ask"]),
        Bid = ConversionManager.Value<double>(input["bid"]),
        AskSize = ConversionManager.Value<double>(input["asksz"]),
        BidSize = ConversionManager.Value<double>(input["bidsz"]),
        Time = DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(dateAsk, dateBid)).DateTime,
        Instrument = new InstrumentModel
        {
          Name = $"{ input["symbol"] }"
        }
      };
    }
  }
}
