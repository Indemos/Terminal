using Alpaca.Mappers;
using Alpaca.Markets;
using Distribution.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

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
    protected IDictionary<string, List<IAlpacaDataSubscription>> subscriptions;

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
      subscriptions = new Dictionary<string, List<IAlpacaDataSubscription>>();
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<ResponseModel<StatusEnum>> Connect()
    {
      return await Response(async () =>
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
        await GetAccount();
        await Task.WhenAll(Account.States.Values.Select(o => Subscribe(o.Instrument)));

        return StatusEnum.Active;
      });
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<StatusEnum>> Subscribe(InstrumentModel instrument)
    {
      async Task subscribe<T>() where T : class, IStreamingDataClient
      {
        await Unsubscribe(instrument);

        Account.States.Get(instrument.Name).Instrument ??= instrument;

        var client = streamingClients[instrument.Type.Value] as T;
        var onPointSub = client.GetQuoteSubscription(instrument.Name);
        var onTradeSub = client.GetTradeSubscription(instrument.Name);

        subscriptions[instrument.Name] = [onPointSub, onTradeSub];

        onPointSub.Received += OnPoint;
        onTradeSub.Received += OnTrade;

        await client.SubscribeAsync(onPointSub);
        await client.SubscribeAsync(onTradeSub);
      }

      return await Response(async () =>
      {
        switch (instrument.Type)
        {
          case InstrumentEnum.Coins: await subscribe<IAlpacaCryptoStreamingClient>(); break;
          case InstrumentEnum.Shares: await subscribe<IAlpacaDataStreamingClient>(); break;
        }

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
        tradingClient?.Dispose();

        dataClients?.Values?.ForEach(o => o?.Dispose());
        streamingClients?.Values?.ForEach(o => o?.Dispose());

        dataClients?.Clear();
        streamingClients?.Clear();

        return Task.FromResult(StatusEnum.Active);
      });
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument)
    {
      async Task unsubscribe<T>() where T : class, ISubscriptionHandler
      {
        foreach (var sub in subscriptions.Get(instrument.Name) ?? [])
        {
          await (streamingClients[instrument.Type.Value] as T).UnsubscribeAsync(sub);
        }
      }

      return await Response(async () =>
      {
        switch (instrument.Type)
        {
          case InstrumentEnum.Coins: await unsubscribe<IAlpacaCryptoStreamingClient>(); break;
          case InstrumentEnum.Shares: await unsubscribe<IAlpacaDataStreamingClient>(); break;
        }

        return StatusEnum.Active;
      });
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    public override async Task<ResponseModel<Terminal.Core.Domains.IAccount>> GetAccount()
    {
      return await Response(async () =>
      {
        var orders = await GetOrders();
        var positions = await GetPositions();
        var account = await tradingClient.GetAccountAsync();

        Account.Balance = (double)account.Equity;
        Account.Orders = orders.Data.GroupBy(o => o.Id).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
        Account.Positions = positions.Data.GroupBy(o => o.Instrument.Name).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
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
      return await Response(async () =>
      {
        var query = new ListOrdersRequest { OrderStatusFilter = OrderStatusFilter.Open };
        var orders = await tradingClient.ListOrdersAsync(query);
        var response = orders.Select(Downstream.GetOrder).ToList();

        return response ?? [];
      });
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<OrderModel>>> GetPositions(ConditionModel criteria = null)
    {
      return await Response(async () =>
      {
        var orders = await tradingClient.ListPositionsAsync();
        var response = orders.Select(Downstream.GetPosition).ToList();

        return response ?? [];
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
        var pages = 50;
        var instrument = criteria.Instrument;
        var items = new List<InstrumentModel>().AsEnumerable();
        var inputs = new OptionChainRequest(instrument.Name)
        {
          OptionsFeed = OptionsFeed.Indicative,
          ExpirationDateGreaterThanOrEqualTo = criteria.MinDate.AsDate(),
          ExpirationDateLessThanOrEqualTo = criteria.MaxDate.AsDate(),
          StrikePriceGreaterThanOrEqualTo = (decimal?)criteria.MinPrice,
          StrikePriceLessThanOrEqualTo = (decimal?)criteria.MaxPrice
        };

        inputs.Pagination.Size = 1000;

        for (var step = 0; step < pages; step++)
        {
          var pageOptions = await GetPageOptions(inputs);

          items = items.Concat(pageOptions.Data);
          inputs.Pagination.Token = pageOptions.Cursor;
          step = string.IsNullOrEmpty(inputs.Pagination.Token) ? pages : step;
        }

        return items
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
        var instrument = criteria.Instrument;
        var name = instrument.Name;
        var point = instrument.Point;
        var currency = instrument.Currency.Name;
        var client = dataClients[instrument.Type.Value];

        switch (instrument.Type)
        {
          case InstrumentEnum.Coins:
            {
              var inputs = new LatestDataListRequest([name]);
              var points = await (client as IAlpacaCryptoDataClient).ListLatestQuotesAsync(inputs);
              point = Downstream.GetPrice(points[name], instrument);
              break;
            }

          case InstrumentEnum.Shares:
            {
              var inputs = new LatestMarketDataListRequest([name]) { Feed = MarketDataFeed.Iex, Currency = currency };
              var points = await (client as IAlpacaDataClient).ListLatestQuotesAsync(inputs);
              point = Downstream.GetPrice(points[name], instrument);
              break;
            }

          case InstrumentEnum.Options:
            {
              var inputs = new LatestOptionsDataRequest([name]) { OptionsFeed = OptionsFeed.Indicative };
              var points = await (client as IAlpacaOptionsDataClient).ListLatestQuotesAsync(inputs);
              point = Downstream.GetPrice(points[name], instrument);
              break;
            }
        }

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
      return await Response(async () =>
      {
        var instrument = criteria.Instrument;
        var name = instrument.Name;
        var currency = instrument.Currency.Name;
        var client = dataClients[instrument.Type.Value];

        switch (instrument.Type)
        {
          case InstrumentEnum.Coins:
            {
              var inputs = new HistoricalCryptoQuotesRequest(name, criteria.MinDate.Value, criteria.MaxDate.Value);
              var points = await (client as IAlpacaCryptoDataClient).ListHistoricalQuotesAsync(inputs);
              return points.Items.Select(o => Downstream.GetPrice(o, instrument)).ToList();
            }

          case InstrumentEnum.Shares:
            {
              var inputs = new HistoricalQuotesRequest(name, criteria.MinDate.Value, criteria.MaxDate.Value) { Feed = MarketDataFeed.Iex, Currency = currency };
              var points = await (client as IAlpacaDataClient).ListHistoricalQuotesAsync(inputs);
              return points.Items.Select(o => Downstream.GetPrice(o, instrument)).ToList();
            }
        }

        return [];
      });
    }

    /// <summary>
    /// Send orders
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<OrderModel>> SendOrder(OrderModel order)
    {
      var response = new ResponseModel<OrderModel>();

      if ((response.Errors = await SubscribeToOrder(order)).Count is 0)
      {
        Account.Orders[order.Id] = order;

        var exOrder = Upstream.GetOrder(order);
        var exResponse = await tradingClient.PostOrderAsync(exOrder);

        order.Id = $"{exResponse.OrderId}";
        order.Status = Downstream.GetStatus(exResponse.OrderStatus);
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
        var o = await Response(async () => await tradingClient.CancelOrderAsync(Guid.Parse(order.Id)));

        response.Errors = [.. response.Errors.Concat(o.Errors)];
        response.Data = [.. response.Data.Append(order)];
      }

      response.Errors = [.. response.Errors.Concat((await GetAccount()).Errors)];

      return response;
    }

    /// <summary>
    /// Process quote from the stream
    /// </summary>
    /// <param name="streamPoint"></param>
    protected virtual void OnPoint(IQuote streamPoint) => InstanceService<ScheduleService>.Instance.Send(() => Observe(() =>
    {
      var summary = Account.States.Get(streamPoint.Symbol);
      var point = Downstream.GetPrice(streamPoint, summary.Instrument);

      point.Account = Account;

      summary.Points.Add(point);
      summary.PointGroups.Add(point, summary.TimeFrame);
      summary.Instrument.Point = summary.PointGroups.Last();

      Stream(new MessageModel<PointModel> { Next = summary.Instrument.Point });
    }));

    /// <summary>
    /// Process quote from the stream
    /// </summary>
    /// <param name="streamOrder"></param>
    protected virtual void OnTrade(ITrade streamOrder) => InstanceService<ScheduleService>.Instance.Send(() => Observe(() =>
    {
      var order = new OrderModel
      {
        Id = $"{streamOrder.TradeId}",
        Time = streamOrder.TimestampUtc,
        Price = (double)streamOrder.Price,
        OpenAmount = (double)streamOrder.Size,
        Side = Downstream.GetTakerSide(streamOrder.TakerSide),
        Name = streamOrder.Symbol
      };

      var message = new MessageModel<OrderModel>
      {
        Next = order
      };

      OrderStream(message);
    }));

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="screener"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<List<InstrumentModel>>> GetPageOptions(OptionChainRequest screener)
    {
      var response = new ResponseModel<List<InstrumentModel>>();
      var client = dataClients[InstrumentEnum.Options] as IAlpacaOptionsDataClient;
      var optionResponse = await client.GetOptionChainAsync(screener);

      response.Cursor = optionResponse.NextPageToken;
      response.Data = optionResponse
        .Items
        .Select(option => Downstream.GetOption(screener, option.Value, option.Key))
        .ToList();

      return response;
    }
  }
}
