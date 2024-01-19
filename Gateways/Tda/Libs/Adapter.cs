using ServiceScheduler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Connector.Tda.Models;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
using Terminal.Core.Services;

namespace Terminal.Connector.Tda
{
  public class Adapter : Terminal.Core.Domains.Connector
  {
    /// <summary>
    /// User
    /// </summary>
    protected UserModel _user;

    /// <summary>
    /// Disposable connections
    /// </summary>
    protected IList<IDisposable> _connections;

    /// <summary>
    /// Disposable subscriptions
    /// </summary>
    protected IList<IDisposable> _subscriptions;

    /// <summary>
    /// Application ID
    /// </summary>
    public virtual string ConsumerKey { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    public virtual string Username { get; set; }

    /// <summary>
    /// Password
    /// </summary>
    public virtual string Password { get; set; }

    /// <summary>
    /// Answer to a secret question
    /// </summary>
    public virtual string Answer { get; set; }

    /// <summary>
    /// Query URL
    /// </summary>
    public virtual string QueryUri { get; set; } = "https://api.tdameritrade.com/v1";

    /// <summary>
    /// Authentication URL
    /// </summary>
    public virtual string SignInRemoteUri { get; set; } = "https://auth.tdameritrade.com/auth";

    /// <summary>
    /// Authentication URL for API
    /// </summary>
    public virtual string SignInApiUri { get; set; } = "https://api.tdameritrade.com/v1/oauth2/token";

    /// <summary>
    /// Local return URL for authentication
    /// </summary>
    public virtual string SignInLocalUri { get; set; } = "http://localhost";

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      _connections = new List<IDisposable>();
      _subscriptions = new List<IDisposable>();
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<IList<ErrorModel>> Connect()
    {
      await Disconnect();

      var automator = new Authenticator
      {
        Location = Path.GetTempPath()
      };

      _user = await automator.SignIn(this);

      await Subscribe();

      return null;
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    public override async Task<IList<ErrorModel>> Subscribe()
    {
      await Unsubscribe();

      return null;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<IList<ErrorModel>> Disconnect()
    {
      Unsubscribe();

      _connections?.ForEach(o => o.Dispose());
      _connections?.Clear();

      return Task.FromResult<IList<ErrorModel>>(null);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    public override Task<IList<ErrorModel>> Unsubscribe()
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
    public override async Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message)
    {
      var props = new Dictionary<object, object>
      {
        ["symbol"] = message.Name,
        ["fromDate"] = $"{message.MinDate:yyyy-MM-dd}",
        ["toDate"] = $"{message.MaxDate:yyyy-MM-dd}",
        ["includeQuotes"] = "TRUE"
      };

      var query = new HttpRequestMessage
      {
        RequestUri = new Uri($"{QueryUri}/marketdata/chains?{props.ToQuery()}")
      };

      query.Headers.Add("Authorization", $"{_user.TokenType} {_user.AccessToken}");

      var service = InstanceService<Service>.Instance;
      var response = await service.Send<OptionChain>(query, service.Options, new CancellationTokenSource());
      var options = response
        .PutExpDateMap
        .Concat(response.CallExpDateMap)
        .SelectMany(dateMap => dateMap.Value.SelectMany(o => o.Value))
        .Select(o =>
        {
          var option = new OptionModel
          {
            Name = o.Symbol,
            BaseName = response.Symbol,
            OpenInterest = o.OpenInterest ?? 0,
            Strike = o.StrikePrice ?? 0,
            IntrinsicValue = o.IntrinsicValue ?? 0,
            Leverage = o.Multiplier ?? 0,
            Volatility = o.Volatility ?? 0,
            Volume = o.TotalVolume ?? 0,
            Point = new PointModel
            {
              Ask = o.Ask ?? 0,
              AskSize = o.AskSize ?? 0,
              Bid = o.Bid ?? 0,
              BidSize = o.BidSize ?? 0,
              Bar = new BarModel
              {
                Low = o.LowPrice ?? 0,
                High = o.LowPrice ?? 0,
                Open = o.OpenPrice ?? 0,
                Close = o.ClosePrice ?? 0
              }
            },
            Derivatives = new DerivativeModel
            {
              Rho = o.Rho ?? 0,
              Vega = o.Vega ?? 0,
              Delta = o.Delta ?? 0,
              Gamma = o.Gamma ?? 0,
              Theta = o.Theta ?? 0
            }
          };

          switch (o.PutCall.ToUpper())
          {
            case "PUT": option.Side = OptionSideEnum.Put; break;
            case "CALL": option.Side = OptionSideEnum.Call; break;
          }

          if (o.ExpirationDate is not null)
          {
            option.ExpirationDate = DateTimeOffset.FromUnixTimeMilliseconds(o.ExpirationDate.Value).UtcDateTime;
          }

          return option;

        }).ToList();

      return new ResponseItemModel<IList<OptionModel>>
      {
        Data = options
      };
    }

    public override Task<ResponseItemModel<IList<PointModel>>> GetPoints(PointMessageModel message)
    {
      throw new NotImplementedException();
    }

    public override Task<ResponseModel<OrderModel>> CreateOrders(params OrderModel[] orders)
    {
      throw new NotImplementedException();
    }

    public override Task<ResponseModel<OrderModel>> UpdateOrders(params OrderModel[] orders)
    {
      throw new NotImplementedException();
    }

    public override Task<ResponseModel<OrderModel>> DeleteOrders(params OrderModel[] orders)
    {
      throw new NotImplementedException();
    }
  }
}
