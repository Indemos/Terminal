using Alpaca.Enums;
using Alpaca.Markets;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Alpaca
{
  public class Adapter : Gateway
  {
    /// <summary>
    /// Disposable connections
    /// </summary>
    protected IList<IDisposable> _connections;

    /// <summary>
    /// Disposable subscriptions
    /// </summary>
    protected IList<IDisposable> _subscriptions;

    /// <summary>
    /// Key
    /// </summary>
    public virtual string ConsumerKey { get; set; }

    /// <summary>
    /// Secret
    /// </summary>
    public virtual string ConsumerSecret { get; set; }

    /// <summary>
    /// API source
    /// </summary>
    public virtual string DataUri { get; set; }

    /// <summary>
    /// Streaming 
    /// </summary>
    public virtual string StreamUri { get; set; }

    /// <summary>
    /// Asset type
    /// </summary>
    public virtual AssetEnum Asset { get; set; }

    /// <summary>
    /// Environment
    /// </summary>
    public virtual IEnvironment Environment { get; set; }

    /// <summary>
    /// Data client
    /// </summary>
    public virtual IAlpacaCryptoDataClient CoinClient { get; protected set; }

    /// <summary>
    /// Data streaming client
    /// </summary>
    public virtual IAlpacaCryptoStreamingClient CoinStreamClient { get; protected set; }

    /// <summary>
    /// Data client
    /// </summary>
    public virtual IAlpacaDataClient DataClient { get; protected set; }

    /// <summary>
    /// Data streaming client
    /// </summary>
    public virtual IAlpacaDataStreamingClient DataStreamClient { get; protected set; }

    /// <summary>
    /// Trading client
    /// </summary>
    public virtual IAlpacaTradingClient TradeClient { get; protected set; }

    /// <summary>
    /// Streaming client
    /// </summary>
    public virtual IAlpacaStreamingClient TradeStreamClient { get; protected set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      DataUri = "https://paper-api.alpaca.markets";
      StreamUri = "wss://paper-api.alpaca.markets/stream";
      Environment = Environments.Paper;

      _connections = new List<IDisposable>();
      _subscriptions = new List<IDisposable>();
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<IList<ErrorModel>> Connect()
    {
      await Disconnect();

      var creds = new SecretKey(ConsumerKey, ConsumerSecret);

      switch (Asset)
      {
        case AssetEnum.Coin:

          CoinClient = Environment.GetAlpacaCryptoDataClient(creds);
          CoinStreamClient = Environment.GetAlpacaCryptoStreamingClient(creds);
          break;

        case AssetEnum.Stock:

          DataClient = Environment.GetAlpacaDataClient(creds);
          DataStreamClient = Environment.GetAlpacaDataStreamingClient(creds);
          break;
      }

      TradeClient = Environment.GetAlpacaTradingClient(creds);
      TradeStreamClient = Environment.GetAlpacaStreamingClient(creds);

      Account.Instruments.ForEach(async o => await Subscribe(o.Key));

      return null;
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    public override async Task<IList<ErrorModel>> Subscribe(string name)
    {
      await Unsubscribe(name);

      return null;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<IList<ErrorModel>> Disconnect()
    {
      _connections?.ForEach(o => o.Dispose());
      _connections?.Clear();

      CoinClient?.Dispose();
      CoinStreamClient?.Dispose();
      DataClient?.Dispose();
      DataStreamClient?.Dispose();
      TradeClient?.Dispose();
      TradeStreamClient?.Dispose();

      return Task.FromResult<IList<ErrorModel>>(null);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    public override Task<IList<ErrorModel>> Unsubscribe(string name)
    {
      _subscriptions?.ForEach(o => o.Dispose());
      _subscriptions?.Clear();

      return Task.FromResult<IList<ErrorModel>>(null);
    }

    /// <summary>
    /// Get quote
    /// </summary>
    /// <param name="message"></param>
    public override Task<ResponseItemModel<PointModel>> GetPoint(PointMessageModel message)
    {
      var response = new ResponseItemModel<PointModel>
      {
        Data = Account.Instruments[message.Name].Points.LastOrDefault()
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Get option chains
    /// </summary>
    /// <param name="message"></param>
    public override Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message)
    {
      throw new NotImplementedException();
    }

    public override Task<ResponseItemModel<IList<PointModel>>> GetPoints(PointMessageModel message)
    {
      throw new NotImplementedException();
    }

    public override async Task<ResponseMapModel<OrderModel>> CreateOrders(params OrderModel[] orders)
    {
      var response = new ResponseMapModel<OrderModel>();

      foreach (var order in orders)
      {
        var name = order.Transaction.Instrument.Name;
        var amount = (int)order.Transaction.Volume;
        var price = (decimal)order.Transaction.Price;
        var isBuy = Equals(order.Side, OrderSideEnum.Buy);
        var isSell = Equals(order.Side, OrderSideEnum.Sell);

        if (Equals(order.Side, OrderSideEnum.Buy))
        {
          switch (order.Type)
          {
            case OrderTypeEnum.Stop: await TradeClient.PostOrderAsync(OrderSide.Buy.Stop(name, amount, price)); break;
            case OrderTypeEnum.Limit: await TradeClient.PostOrderAsync(OrderSide.Buy.Limit(name, amount, price)); break;
            case OrderTypeEnum.StopLimit: await TradeClient.PostOrderAsync(OrderSide.Buy.StopLimit(name, amount, order.ActivationPrice, price); break;
            case OrderTypeEnum.Market: await TradeClient.PostOrderAsync(OrderSide.Buy.Stop(name, amount, price); break;
          }
        }
      }

      return response;
    }

    public override Task<ResponseMapModel<OrderModel>> UpdateOrders(params OrderModel[] orders)
    {
      throw new NotImplementedException();
    }

    public override Task<ResponseMapModel<OrderModel>> DeleteOrders(params OrderModel[] orders)
    {
      throw new NotImplementedException();
    }
  }
}
