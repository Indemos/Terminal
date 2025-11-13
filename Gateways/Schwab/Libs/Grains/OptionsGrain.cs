using Core.Enums;
using Core.Grains;
using Core.Models;
using Schwab.Messages;
using Schwab.Models;
using Schwab.Queries;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Schwab.Grains
{
  public interface ISchwabOptionsGrain : IOptionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(Connection connection);
  }

  public class SchwabOptionsGrain : OptionsGrain, ISchwabOptionsGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected SchwabBroker connector;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection)
    {
      state = connection;
      connector = new()
      {
        ClientId = connection.Id,
        ClientSecret = connection.Secret,
        AccessToken = connection.AccessToken,
        RefreshToken = connection.RefreshToken
      };

      await connector.Connect();

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<InstrumentsResponse> Options(Criteria criteria)
    {
      var query = new ChainQuery
      {
        ToDate = criteria.MaxDate,
        FromDate = criteria.MinDate,
        Symbol = criteria.Instrument.Name,
        StrikeCount = criteria.Count
      };

      var cleaner = new CancellationTokenSource(state.Timeout);
      var chain = await connector.GetOptions(query, cleaner.Token);
      var options = chain
        ?.PutExpDateMap
        ?.Concat(chain?.CallExpDateMap)
        ?.SelectMany(dateMap => dateMap.Value.SelectMany(o => o.Value))
        ?.Select(option => MapOption(option, chain))
        ?.OrderBy(o => o.Derivative.ExpirationDate)
        ?.ThenBy(o => o.Derivative.Strike)
        ?.ThenBy(o => o.Derivative.Side)
        ?.ToList() ?? [];

      return new()
      {
        Data = options
      };
    }

    /// <summary>
    /// Map option type
    /// </summary>
    /// <param name="assetType"></param>
    protected virtual InstrumentEnum? MapOptionType(string assetType)
    {
      switch (assetType?.ToUpper())
      {
        case "COE":
        case "ETF":
        case "INDEX":
        case "EQUITY":
        case "OPTION":
        case "EXTENDED": return InstrumentEnum.Options;
        case "BOND":
        case "FUTURE":
        case "FUTURE_OPTION": return InstrumentEnum.FutureOptions;
      }

      return null;
    }

    /// <summary>
    /// Map instrument
    /// </summary>
    /// <param name="assetType"></param>
    public static InstrumentEnum? MapInstrumentType(string assetType)
    {
      switch (assetType?.ToUpper())
      {
        case "COE":
        case "ETF":
        case "EQUITY":
        case "MUTUAL_FUND": return InstrumentEnum.Shares;
        case "INDEX": return InstrumentEnum.Indices;
        case "BOND": return InstrumentEnum.Bonds;
        case "FOREX": return InstrumentEnum.Currencies;
        case "FUTURE": return InstrumentEnum.Futures;
        case "FUTURE_OPTION": return InstrumentEnum.FutureOptions;
        case "OPTION": return InstrumentEnum.Options;
      }

      return InstrumentEnum.Group;
    }

    /// <summary>
    /// Map option
    /// </summary>
    /// <param name="optionMessage"></param>
    /// <param name="message"></param>
    protected Instrument MapOption(OptionMessage optionMessage, OptionChainMessage message)
    {
      var asset = message.Underlying;
      var price = message.UnderlyingPrice;
      var item = new Price
      {
        Ask = asset?.Ask ?? price,
        Bid = asset?.Bid ?? price,
        AskSize = asset?.AskSize ?? 0,
        BidSize = asset?.BidSize ?? 0,
        Last = asset?.Last ?? price
      };

      var instrument = new Instrument
      {
        Type = MapInstrumentType(message.AssetType),
        Exchange = asset?.ExchangeName,
        Name = message.Symbol,
        Price = item
      };

      var optionItem = new Price
      {
        Ask = optionMessage.Ask,
        Bid = optionMessage.Bid,
        AskSize = optionMessage.AskSize ?? 0,
        BidSize = optionMessage.BidSize ?? 0,
        Volume = optionMessage.TotalVolume ?? 0,
        Last = optionMessage.Last,
      };

      var optionInstrument = new Instrument
      {
        Basis = instrument,
        Price = optionItem,
        Name = optionMessage.Symbol,
        Leverage = optionMessage.Multiplier ?? 100,
        Type = MapOptionType(message.AssetType)
      };

      var variance = new Variance
      {
        Rho = optionMessage.Rho ?? 0,
        Vega = optionMessage.Vega ?? 0,
        Delta = optionMessage.Delta ?? 0,
        Gamma = optionMessage.Gamma ?? 0,
        Theta = optionMessage.Theta ?? 0
      };

      var derivative = new Derivative
      {
        Strike = optionMessage.StrikePrice,
        ExpirationDate = optionMessage.ExpirationDate,
        ExpirationType = Enum.TryParse(optionMessage.ExpirationType, true, out ExpirationTypeEnum o) ? o : null,
        OpenInterest = optionMessage.OpenInterest ?? 0,
        IntrinsicValue = optionMessage.IntrinsicValue ?? 0,
        Volatility = optionMessage.Volatility ?? 0,
        Variance = variance
      };

      if (optionMessage.LastTradingDay is not null)
      {
        derivative = derivative with
        {
          TradeDate = DateTimeOffset
            .FromUnixTimeMilliseconds((long)optionMessage.LastTradingDay)
            .LocalDateTime
        };
      }

      switch (optionMessage?.PutCall?.ToUpper())
      {
        case "PUT": derivative = derivative with { Side = OptionSideEnum.Put }; break;
        case "CALL": derivative = derivative with { Side = OptionSideEnum.Call }; break;
      }

      optionInstrument = optionInstrument with
      {
        Price = optionItem,
        Derivative = derivative
      };

      return optionInstrument;
    }
  }
}
