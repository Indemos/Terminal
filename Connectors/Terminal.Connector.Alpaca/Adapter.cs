using Alpaca.Markets;
using Alpaca.Markets.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;
using Terminal.Core.ServiceSpace;

namespace Terminal.Connector.Alpaca
{
  public class Adapter : ConnectorModel, IDisposable
  {
    /// <summary>
    /// Trading client
    /// </summary>
    protected IAlpacaTradingClient _orderClient;

    /// <summary>
    /// Streaming client
    /// </summary>
    protected IAlpacaStreamingClient _dataClient;

    /// <summary>
    /// Disposable connections
    /// </summary>
    protected IList<IDisposable> _connections;

    /// <summary>
    /// Disposable subscriptions
    /// </summary>
    protected IList<IDisposable> _subscriptions;

    /// <summary>
    /// API key
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// API secret
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      _connections = new List<IDisposable>();
      _subscriptions = new List<IDisposable>();
    }

    /// <summary>
    /// Establish connection with a server
    /// </summary>
    public override async Task Connect()
    {
      try
      {
        await Disconnect();

        //var x = new SecretKey(Token, Secret);
        //var x1 = GetEnvironment().GetAlpacaCryptoStreamingClient(x);
        //var x2 = GetEnvironment().GetAlpacaDataStreamingClient(x);
        //var x3 = GetEnvironment().GetAlpacaNewsStreamingClient(x);
        //var x4 = GetEnvironment().GetAlpacaStreamingClient(x);

        //await x1.ConnectAndAuthenticateAsync();
        //await x2.ConnectAndAuthenticateAsync();
        //await x3.ConnectAndAuthenticateAsync();
        //await x4.ConnectAndAuthenticateAsync();

        //var xx1 = x1.GetMinuteBarSubscription("ETHUSD");
        ////var xxx1 = x1.SubscribeAsync(xx1);
        //xx1.Received += o => { };

        //var xx2 = x2.GetMinuteBarSubscription("SPY");
        ////var xxx2 = x2.SubscribeAsync(xx2);
        //xx2.Received += o => { };

        //var xx3 = x3.GetNewsSubscription("SPY");
        ////var xxx3 = x3.SubscribeAsync(xx3);
        //xx3.Received += o => { };

        //x4.OnTradeUpdate += o => { };

        _dataClient = GetDataClient();
        _orderClient = GetOrderClient();

        var account = await _orderClient.GetAccountAsync();
        var orderStream = OrderStream.Subscribe(async o =>
        {
          var order = o.Next;
          var response = await SendOrder(order);
        });

        await Subscribe();

        _connections.Add(_orderClient);
        _connections.Add(_dataClient);
        _connections.Add(orderStream);
      }
      catch (Exception e)
      {
        IInstanceManager<LogService>.Instance.Log.Error(e.ToString());
      }
    }

    /// <summary>
    /// Start streaming
    /// </summary>
    /// <returns></returns>
    public override async Task Subscribe()
    {
      await Unsubscribe();
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task Disconnect()
    {
      Unsubscribe();

      _connections?.ForEach(o => o.Dispose());
      _connections?.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    public override Task Unsubscribe()
    {
      _subscriptions?.ForEach(o => o.Dispose());
      _subscriptions?.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Get environment
    /// </summary>
    protected virtual IEnvironment GetEnvironment()
    {
      switch (Mode)
      {
        case EnvironmentEnum.Live: return Environments.Live;
        case EnvironmentEnum.Paper: return Environments.Paper;
      }

      return null;
    }

    /// <summary>
    /// Get trading client
    /// </summary>
    protected virtual IAlpacaTradingClient GetOrderClient()
    {
      return GetEnvironment().GetAlpacaTradingClient(new SecretKey(Token, Secret));
    }

    /// <summary>
    /// Get streaming client
    /// </summary>
    protected virtual IAlpacaStreamingClient GetDataClient()
    {
      return GetEnvironment().GetAlpacaStreamingClient(new SecretKey(Token, Secret));
    }

    /// <summary>
    /// Get order side
    /// </summary>
    protected virtual OrderSide? GetOrderSide(OrderSideEnum side)
    {
      switch (side)
      {
        case OrderSideEnum.Buy: return OrderSide.Buy;
        case OrderSideEnum.Sell: return OrderSide.Sell;
      }

      return null;
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual Task<IOrder> SendOrder(ITransactionOrderModel order)
    {
      var size = (long)order.Instrument.Size;
      var name = order.Instrument.Name;
      var side = GetOrderSide(order.Side.Value);

      if (side is not null)
      {
        switch (order.Type)
        {
          case OrderTypeEnum.Market: return _orderClient.PostOrderAsync(side?.Market(name, size));
          case OrderTypeEnum.Stop: return _orderClient.PostOrderAsync(side?.Stop(name, size, (decimal)order.Price));
          case OrderTypeEnum.Limit: return _orderClient.PostOrderAsync(side?.Limit(name, size, (decimal)order.Price));
          case OrderTypeEnum.StopLimit: return _orderClient.PostOrderAsync(side?.StopLimit(name, size, (decimal)order.ActivationPrice, (decimal)order.Price));
        }
      }

      return Task.FromResult<IOrder>(null);
    }
  }
}
