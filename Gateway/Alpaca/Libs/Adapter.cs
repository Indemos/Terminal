using Alpaca.Mappers;
using Alpaca.Markets;
using Distribution.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
using Dom = Terminal.Core.Domains;

namespace Alpaca
{
  public class Adapter : Gateway
  {
    /// <summary>
    /// Trading client
    /// </summary>
    protected IAlpacaTradingClient tradingClient;

    /// <summary>
    /// Data clients
    /// </summary>
    protected IDictionary<InstrumentEnum, IDisposable> dataClients;

    /// <summary>
    /// Streaming clients
    /// </summary>
    protected IDictionary<InstrumentEnum, IStreamingClient> streamingClients;

    /// <summary>
    /// Streams
    /// </summary>
    protected IDictionary<string, IList<IAlpacaDataSubscription>> subscriptions;

    /// <summary>
    /// Client ID
    /// </summary>
    public virtual string Token { get; set; }

    /// <summary>
    /// Client secret
    /// </summary>
    public virtual string Secret { get; set; }

    /// <summary>
    /// Environment
    /// </summary>
    public virtual IEnvironment Source { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      Source = Environments.Paper;

      dataClients = new Dictionary<InstrumentEnum, IDisposable>();
      streamingClients = new Dictionary<InstrumentEnum, IStreamingClient>();
      subscriptions = new Dictionary<string, IList<IAlpacaDataSubscription>>();
    }

    /// <summary>
    /// Check active connections
    /// </summary>
    /// <returns></returns>
    public override bool IsConnected() => dataClients.Any();

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<ResponseModel<StatusEnum>> Connect()
    {
      var response = new ResponseModel<StatusEnum>();

      try
      {
        await Disconnect();

        var creds = new SecretKey(Token, Secret);

        tradingClient = Source.GetAlpacaTradingClient(creds);
        dataClients[InstrumentEnum.Shares] = Source.GetAlpacaDataClient(creds);
        dataClients[InstrumentEnum.Coins] = Source.GetAlpacaCryptoDataClient(creds);
        dataClients[InstrumentEnum.Options] = Source.GetAlpacaOptionsDataClient(creds);
        streamingClients[InstrumentEnum.Shares] = Source.GetAlpacaDataStreamingClient(creds);
        streamingClients[InstrumentEnum.Coins] = Source.GetAlpacaCryptoStreamingClient(creds);
        streamingClients[InstrumentEnum.None] = Source.GetAlpacaStreamingClient(creds);

        await Task.WhenAll(streamingClients.Values.Select(o => o.ConnectAndAuthenticateAsync()));
        await GetAccount([]);
        await Task.WhenAll(Account.State.Values.Select(o => Subscribe(o.Instrument)));

        response.Data = StatusEnum.Active;
      }
      catch (Exception e)
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return response;
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<StatusEnum>> Subscribe(InstrumentModel instrument)
    {
      var response = new ResponseModel<StatusEnum>();

      async Task subscribe<T>() where T : class, IStreamingDataClient
      {
        await Unsubscribe(instrument);

        var client = streamingClients[instrument.Type.Value] as T;
        var onPointSub = client.GetQuoteSubscription(instrument.Name);
        var onTradeSub = client.GetTradeSubscription(instrument.Name);

        subscriptions[instrument.Name] = [onPointSub, onTradeSub];

        onPointSub.Received += OnPoint;
        onTradeSub.Received += OnTrade;

        await client.SubscribeAsync(onPointSub);
        await client.SubscribeAsync(onTradeSub);
      }

      try
      {
        switch (instrument.Type)
        {
          case InstrumentEnum.Coins: await subscribe<IAlpacaCryptoStreamingClient>(); break;
          case InstrumentEnum.Shares: await subscribe<IAlpacaDataStreamingClient>(); break;
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
    /// Save state and dispose
    /// </summary>
    public override Task<ResponseModel<StatusEnum>> Disconnect()
    {
      var response = new ResponseModel<StatusEnum>();

      try
      {
        tradingClient?.Dispose();

        dataClients?.Values?.ForEach(o => o?.Dispose());
        streamingClients?.Values?.ForEach(o => o?.Dispose());

        dataClients?.Clear();
        streamingClients?.Clear();

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
    public override async Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument)
    {
      var response = new ResponseModel<StatusEnum>();

      async Task unsubscribe<T>() where T : class, ISubscriptionHandler
      {
        if (subscriptions.TryGetValue(instrument.Name, out var subs))
        {
          foreach (var sub in subs)
          {
            await (streamingClients[instrument.Type.Value] as T).UnsubscribeAsync(sub);
          }
        }
      }

      try
      {
        switch (instrument.Type)
        {
          case InstrumentEnum.Coins: await unsubscribe<IAlpacaCryptoStreamingClient>(); break;
          case InstrumentEnum.Shares: await unsubscribe<IAlpacaDataStreamingClient>(); break;
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
    /// Sync open balance, order, and positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<Dom.IAccount>> GetAccount(Hashtable criteria)
    {
      var response = new ResponseModel<Dom.IAccount>();

      try
      {
        var orders = await GetOrders(null, criteria);
        var positions = await GetPositions(null, criteria);
        var account = await tradingClient.GetAccountAsync();

        Account.Balance = (double)account.Equity;
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

      try
      {
        var props = new ListOrdersRequest { OrderStatusFilter = OrderStatusFilter.Open };
        var items = await tradingClient.ListOrdersAsync(props);

        response.Data = items.Select(InternalMap.GetOrder)?.ToList() ?? [];
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
    public override async Task<ResponseModel<IList<OrderModel>>> GetPositions(PositionScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<OrderModel>>();

      try
      {
        var items = await tradingClient.ListPositionsAsync();

        response.Data = items.Select(InternalMap.GetPosition)?.ToList() ?? [];
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<InstrumentModel>>> GetOptions(InstrumentScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<InstrumentModel>>();

      try
      {
        var pages = 50;
        var items = new List<InstrumentModel>().AsEnumerable();
        var inputs = new OptionChainRequest(screener.Instrument.Name)
        {
          OptionsFeed = OptionsFeed.Indicative,
          ExpirationDateGreaterThanOrEqualTo = screener.MinDate.AsDate(),
          ExpirationDateLessThanOrEqualTo = screener.MaxDate.AsDate(),
          StrikePriceGreaterThanOrEqualTo = (decimal?)screener.MinPrice,
          StrikePriceLessThanOrEqualTo = (decimal?)screener.MaxPrice
        };

        inputs.Pagination.Size = 1000;

        for (var step = 0; step < pages; step++)
        {
          var pageOptions = await GetPageOptions(inputs);

          items = items.Concat(pageOptions.Data);
          inputs.Pagination.Token = pageOptions.Cursor;
          step = string.IsNullOrEmpty(inputs.Pagination.Token) ? pages : step;
        }

        items = items
          .OrderBy(o => o.Derivative.ExpirationDate)
          .ThenBy(o => o.Derivative.Strike)
          .ThenBy(o => o.Derivative.Side);

        response.Data = [.. items];
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
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<DomModel>> GetDom(PointScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<DomModel>();

      try
      {
        var name = screener.Instrument.Name;
        var point = screener.Instrument.Point;
        var currency = screener.Instrument.Currency.Name;
        var client = dataClients[screener.Instrument.Type.Value];

        switch (screener.Instrument.Type)
        {
          case InstrumentEnum.Coins:
            {
              var inputs = new LatestDataListRequest([name]);
              var points = await (client as IAlpacaCryptoDataClient).ListLatestQuotesAsync(inputs);
              point = InternalMap.GetPrice(points[name], screener.Instrument);
            }
            break;

          case InstrumentEnum.Shares:
            {
              var inputs = new LatestMarketDataListRequest([name]) { Feed = MarketDataFeed.Iex, Currency = currency };
              var points = await (client as IAlpacaDataClient).ListLatestQuotesAsync(inputs);
              point = InternalMap.GetPrice(points[name], screener.Instrument);
            }
            break;

          case InstrumentEnum.Options:
            {
              var inputs = new LatestOptionsDataRequest([name]) { OptionsFeed = OptionsFeed.Indicative };
              var points = await (client as IAlpacaOptionsDataClient).ListLatestQuotesAsync(inputs);
              point = InternalMap.GetPrice(points[name], screener.Instrument);
            }
            break;
        }

        response.Data = new DomModel
        {
          Asks = [point],
          Bids = [point],
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
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<PointModel>>> GetPoints(PointScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<PointModel>>();

      try
      {
        var name = screener.Instrument.Name;
        var currency = screener.Instrument.Currency.Name;
        var client = dataClients[screener.Instrument.Type.Value];

        switch (screener.Instrument.Type)
        {
          case InstrumentEnum.Coins:
            {
              var inputs = new HistoricalCryptoQuotesRequest(name, screener.MinDate.Value, screener.MaxDate.Value);
              var points = await (client as IAlpacaCryptoDataClient).ListHistoricalQuotesAsync(inputs);
              response.Data = [.. points.Items.Select(o => InternalMap.GetPrice(o, screener.Instrument))];
            }
            break;

          case InstrumentEnum.Shares:
            {
              var inputs = new HistoricalQuotesRequest(name, screener.MinDate.Value, screener.MaxDate.Value) { Feed = MarketDataFeed.Iex, Currency = currency };
              var points = await (client as IAlpacaDataClient).ListHistoricalQuotesAsync(inputs);
              response.Data = [.. points.Items.Select(o => InternalMap.GetPrice(o, screener.Instrument))];
            }
            break;
        }
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
    public override async Task<ResponseModel<IList<OrderModel>>> CreateOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<IList<OrderModel>> { Data = [] };

      foreach (var order in orders)
      {
        try
        {
          var subOrders = ComposeOrders(order);

          foreach (var subOrder in subOrders)
          {
            response.Data.Add((await CreateOrder(subOrder)).Data);
          }
        }
        catch (Exception e)
        {
          response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
        }
      }

      await GetAccount([]);

      return response;
    }

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> DeleteOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<IList<OrderModel>>();

      foreach (var order in orders)
      {
        await tradingClient.CancelOrderAsync(Guid.Parse(order.Transaction.Id));
      }

      await GetAccount([]);

      return response;
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<OrderModel>> CreateOrder(OrderModel order)
    {
      Account.Orders[order.Id] = order;

      await Subscribe(order.Transaction.Instrument);

      var exOrder = ExternalMap.GetOrder(order);
      var response = new ResponseModel<OrderModel>();
      var exResponse = await tradingClient.PostOrderAsync(exOrder);

      order.Transaction.Id = $"{exResponse.OrderId}";
      order.Transaction.Status = InternalMap.GetStatus(exResponse.OrderStatus);

      return response;
    }

    /// <summary>
    /// Process quote from the stream
    /// </summary>
    /// <param name="streamPoint"></param>
    protected virtual void OnPoint(IQuote streamPoint)
    {
      var scheduler = InstanceService<ScheduleService>.Instance;

      scheduler.Send(() =>
      {
        var summary = Account.State.Get(streamPoint.Symbol);
        var instrument = summary.Instrument ?? new InstrumentModel();
        var point = InternalMap.GetPrice(streamPoint, instrument);

        summary.Points.Add(point);
        summary.PointGroups.Add(point, instrument.TimeFrame);
        instrument.Name = streamPoint.Symbol;
        instrument.Point = summary.PointGroups.Last();

        DataStream(new MessageModel<PointModel> { Next = instrument.Point });
      });
    }

    /// <summary>
    /// Process quote from the stream
    /// </summary>
    /// <param name="streamOrder"></param>
    protected virtual void OnTrade(ITrade streamOrder)
    {
      var action = new TransactionModel
      {
        Id = $"{streamOrder.TradeId}",
        Time = streamOrder.TimestampUtc,
        Price = (double)streamOrder.Price,
        Volume = (double)streamOrder.Size,
        Instrument = new InstrumentModel { Name = streamOrder.Symbol }
      };

      var order = new OrderModel
      {
        Side = InternalMap.GetTakerSide(streamOrder.TakerSide)
      };

      var message = new MessageModel<OrderModel>
      {
        Next = order
      };

      OrderStream(message);
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="screener"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<IList<InstrumentModel>>> GetPageOptions(OptionChainRequest screener)
    {
      var response = new ResponseModel<IList<InstrumentModel>>();
      var client = dataClients[InstrumentEnum.Options] as IAlpacaOptionsDataClient;
      var optionResponse = await client.GetOptionChainAsync(screener);

      response.Cursor = optionResponse.NextPageToken;
      response.Data = optionResponse
        .Items
        .Select(option => InternalMap.GetOption(screener, option.Value, option.Key))
        .ToList();

      return response;
    }
  }
}
