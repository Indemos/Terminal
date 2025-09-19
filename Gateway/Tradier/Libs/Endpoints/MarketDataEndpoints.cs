using Flurl;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tradier.Messages.MarketData;

namespace Tradier
{
  public partial class Adapter
  {
    /// <summary>
    /// Get all quotes in an option chain
    /// </summary>
    public async Task<OptionsMessage> GetOptionChain(string symbol, DateTime? expiration, bool greeks = true)
    {
      var data = new Hashtable
      {
        { "symbol", symbol },
        { "expiration", $"{expiration:yyyy-MM-dd}" },
        { "greeks", greeks }
      };

      var source = $"{DataUri}/markets/options/chains".SetQueryParams(data);
      var response = await Send<OptionChainCoreMessage>(source);

      return response.Options;
    }

    /// <summary>
    /// Get expiration dates for a particular underlying
    /// </summary>
    public async Task<ExpirationsMessage> GetOptionExpirations(string symbol, bool? includeRoots = true, bool? strikes = true)
    {
      var data = new Hashtable
      {
        { "symbol", symbol },
        { "includeAllRoots", includeRoots },
        { "strikes", strikes }
      };

      var source = $"{DataUri}/markets/options/expirations".SetQueryParams(data);
      var response = await Send<OptionExpirationsCoreMessage>(source);

      return response.Expirations;
    }

    /// <summary>
    /// Get a list of symbols using a keyword lookup on the symbols description
    /// </summary>
    public async Task<QuotesMessage> GetQuotes(IList<string> symbols, bool greeks = true)
    {
      var data = new Hashtable
      {
        { "symbols", string.Join(",", symbols) },
        { "greeks", greeks }
      };

      var source = $"{DataUri}/markets/quotes".SetQueryParams(data);
      var response = await Send<QuotesCoreMessage>(source);

      return response.Quotes;
    }

    /// <summary>
    /// Get historical pricing for a security
    /// </summary>
    public async Task<HistoricalQuotesMessage> GetHistoricalQuotes(string symbol, string interval, DateTime start, DateTime end)
    {
      var data = new Hashtable
      {
        { "symbol", symbol },
        { "interval", interval },
        { "start", $"{start:yyyy-MM-dd}" },
        { "end", $"{end:yyyy-MM-dd}" }
      };

      var source = $"{DataUri}/markets/history".SetQueryParams(data);
      var response = await Send<HistoricalQuotesCoreMessage>(source);

      return response.History;
    }

    /// <summary>
    /// Get an options strike prices for a specified expiration date
    /// </summary>
    public async Task<StrikesMessage> GetStrikes(string symbol, DateTime expiration)
    {
      var data = new Hashtable
      {
        { "symbol", symbol },
        { "expiration", $"{expiration:yyyy-MM-dd}" }
      };

      var source = $"{DataUri}/markets/options/strikes".SetQueryParams(data);
      var response = await Send<OptionStrikesCoreMessage>(source);

      return response.Strikes;
    }

    /// <summary>
    /// Time and Sales (timesales) is typically used for charting purposes. It captures pricing across a time slice at predefined intervals.
    /// </summary>
    public async Task<SeriesMessage> GetTimeSales(string symbol, string interval, DateTime start, DateTime end, string filter = "all")
    {
      var data = new Hashtable
      {
        { "symbol", symbol },
        { "interval", interval },
        { "session_filter", filter },
        { "symbol", symbol },
        { "start", $"{start:yyyy-MM-dd HH:mm}" },
        { "end", $"{end:yyyy-MM-dd HH:mm}" }
      };

      var source = $"{DataUri}/markets/timesales".SetQueryParams(data);
      var response = await Send<TimeSalesCoreMessage>(source);

      return response.Series;
    }

    /// <summary>
    /// The ETB list contains securities that are able to be sold short with a Tradier Brokerage account.
    /// </summary>
    public async Task<SecuritiesMessage> GetEtbSecurities()
    {
      var source = $"{DataUri}/markets/etb";
      var response = await Send<SecuritiesCoreMessage>(source);
      return response.Securities;
    }

    /// <summary>
    /// The ETB list contains securities that are able to be sold short with a Tradier Brokerage account.
    /// </summary>
    public async Task<ClockMessage> GetClock()
    {
      var source = $"{DataUri}/markets/clock";
      var response = await Send<ClockCoreMessage>(source);
      return response.Clock;
    }

    /// <summary>
    /// Get the market calendar for the current or given month
    /// </summary>
    public async Task<CalendarMessage> GetCalendar(int? month = null, int? year = null)
    {
      var data = new Hashtable
      {
        { "month", month },
        { "year", year }
      };

      var source = $"{DataUri}/markets/calendar".SetQueryParams(data);
      var response = await Send<CalendarCoreMessage>(source);

      return response.Calendar;
    }

    /// <summary>
    /// Get the market calendar for the current or given month
    /// </summary>
    public async Task<SecuritiesMessage> SearchCompanies(string query, bool indexes = false)
    {
      var data = new Hashtable
      {
        { "q", query },
        { "indexes", indexes }
      };

      var source = $"{DataUri}/markets/search".SetQueryParams(data);
      var response = await Send<SecuritiesCoreMessage>(source);

      return response.Securities;
    }

    /// <summary>
    /// Search for a symbol using the ticker symbol or partial symbol
    /// </summary>
    public async Task<SecuritiesMessage> LookupSymbol(string query, string exchanges = null, string types = null)
    {
      var source = $"{DataUri}/markets/lookup?q={query}";

      source += string.IsNullOrEmpty(exchanges) ? string.Empty : $"&exchanges={exchanges}";
      source += string.IsNullOrEmpty(types) ? string.Empty : $"&types={types}";

      var response = await Send<SecuritiesCoreMessage>(source);
      return response.Securities;
    }

    /// <summary>
    /// Get all options symbols for the given underlying
    /// </summary>
    public async Task<List<SymbolMessage>> LookupOptionSymbols(string symbol)
    {
      var data = new Hashtable
      {
        { "underlying", symbol }
      };

      var source = $"{DataUri}/markets/options/lookup".SetQueryParams(data);
      var response = await Send<OptionSymbolsCoreMessage>(source);

      return response.Symbols;
    }

    /// <summary>
    /// Fundamentals
    /// </summary>
    /// <param name="symbols"></param>
    /// <returns></returns>
    public async Task<List<CompanyDataMessage>> GetCompany(string symbols)
    {
      var data = new Hashtable
      {
        { "symbols", symbols }
      };

      var source = $"{DataUri}/beta/markets/fundamentals/company".SetQueryParams(data);
      var response = await Send<CompanyDataCoreMessage>(source);

      return response.Results;
    }

    /// <summary>
    /// Calendars
    /// </summary>
    /// <param name="symbols"></param>
    /// <returns></returns>
    public async Task<List<CorporateCalendarDataMessage>> GetCorporateCalendars(string symbols)
    {
      var data = new Hashtable
      {
        { "symbols", symbols }
      };

      var source = $"{DataUri}/beta/markets/fundamentals/calendars".SetQueryParams(data);
      var response = await Send<CorporateCalendarCoreMessage>(source);

      return response.Results;
    }
  }
}
