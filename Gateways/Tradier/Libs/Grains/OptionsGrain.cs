using Core.Enums;
using Core.Grains;
using Core.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tradier.Messages.MarketData;
using Tradier.Models;
using Tradier.Queries.MarketData;

namespace Tradier.Grains
{
  public interface ITradierOptionsGrain : IOptionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(Connection connection);
  }

  public class TradierOptionsGrain : OptionsGrain, ITradierOptionsGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected TradierBroker connector;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection)
    {
      var cleaner = new CancellationTokenSource(connection.Timeout);

      state = connection;
      connector = new()
      {
        Token = connection.AccessToken,
        SessionToken = connection.SessionToken,
      };

      await connector.Connect(cleaner.Token);

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
      var query = new OptionChainRequest
      {
        Symbol = criteria.Instrument.Name,
        Expiration = criteria.MaxDate ?? criteria.MinDate
      };

      var cleaner = new CancellationTokenSource(state.Timeout);
      var chain = await connector.GetOptionChain(query, cleaner.Token);
      var options = chain
        .Options
        ?.Select(MapOption)
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
    /// Get internal option
    /// </summary>
    /// <param name="message"></param>
    protected virtual Instrument MapOption(OptionMessage message)
    {
      var instrument = new Instrument
      {
        Name = message.Underlying,
        Exchange = message.Exchange
      };

      var optionPoint = new Price
      {
        Ask = message.Ask,
        Bid = message.Bid,
        AskSize = message.AskSize ?? 0,
        BidSize = message.BidSize ?? 0,
        Volume = message.Volume,
        Last = message.Last,
        Bar = new()
        {
          Low = message.Low,
          High = message.High,
          Open = message.Open,
          Close = message.Close
        }
      };

      var optionInstrument = new Instrument
      {
        Basis = instrument,
        Price = optionPoint,
        Name = message.Symbol,
        Exchange = message.Exchange,
        Leverage = message.ContractSize ?? 100,
        Type = GetInstrumentType(message.Type)
      };

      var side = null as OptionSideEnum?;

      switch (message.OptionType.ToUpper())
      {
        case "PUT": side = OptionSideEnum.Put; break;
        case "CALL": side = OptionSideEnum.Call; break;
      }

      var derivative = new Derivative
      {
        Side = side,
        Strike = message.Strike,
        TradeDate = message.ExpirationDate,
        ExpirationDate = message.ExpirationDate,
        OpenInterest = message.OpenInterest ?? 0,
        Volatility = message?.Greeks?.SmvIV ?? 0,
      };

      var greeks = message?.Greeks;

      if (greeks is not null)
      {
        derivative = derivative with
        {
          Variance = new Variance
          {
            Rho = greeks.Rho ?? 0,
            Vega = greeks.Vega ?? 0,
            Delta = greeks.Delta ?? 0,
            Gamma = greeks.Gamma ?? 0,
            Theta = greeks.Theta ?? 0
          }
        };
      }

      optionInstrument = optionInstrument with
      {
        Price = optionPoint,
        Derivative = derivative
      };

      return optionInstrument;
    }

    /// <summary>
    /// Asset type
    /// </summary>
    /// <param name="assetType"></param>
    protected virtual InstrumentEnum? GetInstrumentType(string assetType)
    {
      switch (assetType?.ToUpper())
      {
        case "EQUITY": return InstrumentEnum.Shares;
        case "INDEX": return InstrumentEnum.Indices;
        case "FUTURE": return InstrumentEnum.Futures;
        case "OPTION": return InstrumentEnum.Options;
      }

      return null;
    }
  }
}
