using Core.EnumSpace;
using Core.ManagerSpace;
using Core.ModelSpace;
using Gateway.Tradier.ModelSpace;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Websocket.Client;

namespace Gateway.Tradier
{
  /// <summary>
  /// Implementation
  /// </summary>
  public class GatewayClient : GatewayModel, IGatewayModel
  {
    /// <summary>
    /// Socket session ID
    /// </summary>
    protected string _streamSession = null;

    /// <summary>
    /// API key
    /// </summary>
    public string Token
    {
      get
      {
        switch (Mode)
        {
          case EnvironmentEnum.Live: return LiveToken;
          case EnvironmentEnum.Sandbox: return SandboxToken;
        }

        return null;
      }
    }

    /// <summary>
    /// HTTP endpoint
    /// </summary>
    public string Source
    {
      get
      {
        switch (Mode)
        {
          case EnvironmentEnum.Live: return LiveSource;
          case EnvironmentEnum.Sandbox: return SandboxSource;
        }

        return null;
      }
    }

    /// <summary>
    /// API key
    /// </summary>
    public string LiveToken { get; set; }

    /// <summary>
    /// Sandbox API key
    /// </summary>
    public string SandboxToken { get; set; }

    /// <summary>
    /// HTTP endpoint
    /// </summary>
    public string LiveSource { get; set; } = "https://api.tradier.com";

    /// <summary>
    /// Sandbox HTTP endpoint
    /// </summary>
    public string SandboxSource { get; set; } = "https://sandbox.tradier.com";

    /// <summary>
    /// Socket endpoint
    /// </summary>
    public string StreamSource { get; set; } = "wss://ws.tradier.com/v1";

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

      _streamSession = await GetStreamSession();

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

        switch ($"{ input.type }")
        {
          case "quote":

            OnInputQuote(ConversionManager.Deserialize<InputPointModel>(message.Text));
            break;

          case "trade": break;
          case "tradex": break;
          case "summary": break;
          case "timesale": break;
        }
      });

      _subscriptions.Add(messageSubscription);
      _subscriptions.Add(connectionSubscription);
      _subscriptions.Add(disconnectionSubscription);
      _subscriptions.Add(client);

      await client.Start();

      var query = new
      {
        linebreak = true,
        advancedDetails = true,
        sessionid = _streamSession,
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
    protected void OnInputQuote(InputPointModel input)
    {
      var dateAsk = input.AskDate;
      var dateBid = input.BidDate;
      var currentAsk = input.Ask;
      var currentBid = input.Bid;
      var previousAsk = _point?.Ask ?? currentAsk;
      var previousBid = _point?.Bid ?? currentBid;
      var symbol = input.Symbol;

      var point = new PointModel
      {
        Ask = currentAsk,
        Bid = currentBid,
        Bar = new PointBarModel(),
        Instrument = Account.Instruments[symbol],
        AskSize = input.AskSize,
        BidSize = input.BidSize,
        Time = DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(dateAsk.Value, dateBid.Value)).DateTime,
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
    /// Get options chain
    /// </summary>
    /// <param name="inputs"></param>
    /// <returns></returns>
    protected async Task<IList<IInstrumentOptionModel>> GetOptionsChain(dynamic inputs)
    {
      var options = new List<IInstrumentOptionModel>();
      var response = await GetResponse<InputOptionItemModel>($"/v1/markets/options/chains");

      foreach (var inputOption in response.Options)
      {
        var optionModel = new InstrumentOptionModel
        {
          Bid = inputOption.Bid,
          Ask = inputOption.Ask,
          Price = inputOption.Last,
          Strike = inputOption.Strike
        };

        options.Add(optionModel);
      }

      return options;
    }

    /// <summary>
    /// Create session to start streaming
    /// </summary>
    /// <returns></returns>
    protected async Task<string> GetStreamSession()
    {
      using (var sessionClient = new ClientService())
      {
        var headers = new Dictionary<dynamic, dynamic>
        {
          ["Accept"] = "application/json",
          ["Authorization"] = $"Bearer { LiveToken }"
        };

        dynamic session = await sessionClient.Post<dynamic>(LiveSource + "/v1/markets/events/session", null, headers);

        return $"{ session.stream.sessionid }";
      }
    }

    /// <summary>
    /// Send HTTP query
    /// </summary>
    /// <param name="source"></param>
    /// <param name="inputs"></param>
    /// <param name="variables"></param>
    /// <returns></returns>
    protected async Task<T> GetResponse<T>(
      string endpoint,
      IDictionary<dynamic, dynamic> inputs = null,
      IDictionary<dynamic, dynamic> variables = null)
    {
      var headers = new Dictionary<dynamic, dynamic>
      {
        ["Accept"] = "application/json",
        ["Authorization"] = $"Bearer { Token }"
      };

      return variables == null ?
        await _serviceClient.Get<T>(Source + endpoint + "?" + ConversionManager.GetQuery(inputs), null, headers) :
        await _serviceClient.Post<T>(Source + endpoint + "?" + ConversionManager.GetQuery(inputs), variables, headers);
    }
  }
}
