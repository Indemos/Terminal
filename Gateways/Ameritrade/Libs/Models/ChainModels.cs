using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Terminal.Gateway.Ameritrade.Models
{
  public struct Option
  { 
    [JsonPropertyName("putCall")]
    public string PutCall { get; set; }
 
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("exchangeName")]
    public string ExchangeName { get; set; }

    [JsonPropertyName("bid")]
    public decimal? Bid { get; set; }

    [JsonPropertyName("ask")]
    public decimal? Ask { get; set; }

    [JsonPropertyName("last")]
    public decimal? Last { get; set; }

    [JsonPropertyName("mark")]
    public decimal? Mark { get; set; }

    [JsonPropertyName("bidSize")]
    public decimal? BidSize { get; set; }

    [JsonPropertyName("askSize")]
    public decimal? AskSize { get; set; }

    [JsonPropertyName("bidAskSize")]
    public string BidAskSize { get; set; }

    [JsonPropertyName("lastSize")]
    public decimal? LastSize { get; set; }

    [JsonPropertyName("highPrice")]
    public decimal? HighPrice { get; set; }

    [JsonPropertyName("lowPrice")]
    public decimal? LowPrice { get; set; }

    [JsonPropertyName("openPrice")]
    public decimal? OpenPrice { get; set; }

    [JsonPropertyName("closePrice")]
    public decimal? ClosePrice { get; set; }

    [JsonPropertyName("totalVolume")]
    public decimal? TotalVolume { get; set; }

    [JsonPropertyName("tradeDate")]
    public DateTime TradeDate { get; set; }

    [JsonPropertyName("quoteTimeInLong")]
    public long? QuoteTimeInLong { get; set; }

    [JsonPropertyName("tradeTimeInLong")]
    public long? TradeTimeInLong { get; set; }

    [JsonPropertyName("netChange")]
    public decimal? NetChange { get; set; }

    [JsonPropertyName("volatility")]
    public decimal? Volatility { get; set; }

    [JsonPropertyName("delta")]
    public decimal? Delta { get; set; }

    [JsonPropertyName("gamma")]
    public decimal? Gamma { get; set; }

    [JsonPropertyName("theta")]
    public decimal? Theta { get; set; }

    [JsonPropertyName("vega")]
    public decimal? Vega { get; set; }

    [JsonPropertyName("rho")]
    public decimal? Rho { get; set; }

    [JsonPropertyName("timeValue")]
    public decimal? TimeValue { get; set; }

    [JsonPropertyName("openInterest")]
    public decimal? OpenInterest { get; set; }

    [JsonPropertyName("inTheMoney")]
    public bool? IsInTheMoney { get; set; }

    [JsonPropertyName("theoreticalOptionValue")]
    public decimal? TheoreticalOptionValue { get; set; }

    [JsonPropertyName("theoreticalVolatility")]
    public decimal? TheoreticalVolatility { get; set; }

    [JsonPropertyName("mini")]
    public bool? IsMini { get; set; }

    [JsonPropertyName("nonStandard")]
    public bool? IsNonStandard { get; set; }

    [JsonPropertyName("optionDeliverablesList")]
    public List<OptionDeliverables> OptionDeliverablesList { get; set; }

    [JsonPropertyName("strikePrice")]
    public decimal? StrikePrice { get; set; }

    [JsonPropertyName("lastTradingDay")]
    public decimal? LastTradingDay { get; set; }

    [JsonPropertyName("expirationDate")]
    public long? ExpirationDate { get; set; }

    [JsonPropertyName("expirationType")]
    public string ExpirationType { get; set; }

    [JsonPropertyName("multiplier")]
    public decimal? Multiplier { get; set; }

    [JsonPropertyName("intrinsicValue")]
    public decimal? IntrinsicValue { get; set; }

    [JsonPropertyName("settlementType")]
    public string SettlementType { get; set; }

    [JsonPropertyName("deliverableNote")]
    public string DeliverableNote { get; set; }

    [JsonPropertyName("isIndexOption")]
    public bool? IsIndexOption { get; set; }

    [JsonPropertyName("pennyPilot")]
    public bool? IsPennyPilot { get; set; }

    [JsonPropertyName("percentChange")]
    public decimal? PercentChange { get; set; }

    [JsonPropertyName("markChange")]
    public decimal? MarkChange { get; set; }

    [JsonPropertyName("markPercentChange")]
    public decimal? MarkPercentChange { get; set; }
  }

  public struct OptionDeliverables
  {    
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; }
    
    [JsonPropertyName("assetType")]
    public string AssetType { get; set; }
    
    [JsonPropertyName("deliverableUnits")]
    public string DeliverableUnits { get; set; }
    
    [JsonPropertyName("currencyType")]
    public string CurrencyType { get; set; }
  }

  public struct OptionChain
  {    
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; }
    
    [JsonPropertyName("underlying")]
    public Underlying? Underlying { get; set; }
    
    [JsonPropertyName("strategy")]
    public string Strategy { get; set; }
    
    [JsonPropertyName("interval")]
    public decimal? Interval { get; set; }
    
    [JsonPropertyName("isDelayed")]
    public bool? IsDelayed { get; set; }
    
    [JsonPropertyName("isIndex")]
    public bool? IsIndex { get; set; }
    
    [JsonPropertyName("daysToExpiration")]
    public decimal? DaysToExpiration { get; set; }

    [JsonPropertyName("interestRate")]
    public decimal? InterestRate { get; set; }
    
    [JsonPropertyName("underlyingPrice")]
    public decimal? UnderlyingPrice { get; set; }
    
    [JsonPropertyName("volatility")]
    public decimal? Volatility { get; set; }
    
    [JsonPropertyName("callExpDateMap")]
    public Dictionary<string, Dictionary<string, List<Option>>> CallExpDateMap { get; set; }
    
    [JsonPropertyName("putExpDateMap")]
    public Dictionary<string, Dictionary<string, List<Option>>> PutExpDateMap { get; set; }
  }

  public struct Underlying
  {    
    [JsonPropertyName("ask")]
    public decimal? Ask { get; set; }
    
    [JsonPropertyName("askSize")]
    public decimal? AskSize { get; set; }
    
    [JsonPropertyName("bid")]
    public decimal? Bid { get; set; }
    
    [JsonPropertyName("bidSize")]
    public decimal? BidSize { get; set; }
    
    [JsonPropertyName("change")]
    public decimal? Change { get; set; }
    
    [JsonPropertyName("close")]
    public decimal? Close { get; set; }
    
    [JsonPropertyName("delayed")]
    public bool? Delayed { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("exchangeName")]
    public string ExchangeName { get; set; }
    
    [JsonPropertyName("fiftyTwoWeekHigh")]
    public decimal? FiftyTwoWeekHigh { get; set; }
    
    [JsonPropertyName("fiftyTwoWeekLow")]
    public decimal? FiftyTwoWeekLow { get; set; }
    
    [JsonPropertyName("highPrice")]
    public decimal? HighPrice { get; set; }
    
    [JsonPropertyName("last")]
    public decimal? Last { get; set; }
    
    [JsonPropertyName("lowPrice")]
    public decimal? LowPrice { get; set; }
    
    [JsonPropertyName("mark")]
    public decimal? Mark { get; set; }
    
    [JsonPropertyName("markChange")]
    public decimal? MarkChange { get; set; }
    
    [JsonPropertyName("markPercentChange")]
    public decimal? MarkPercentChange { get; set; }
    
    [JsonPropertyName("openPrice")]
    public decimal? OpenPrice { get; set; }
    
    [JsonPropertyName("percentChange")]
    public decimal? PercentChange { get; set; }
    
    [JsonPropertyName("quoteTime")]
    public decimal? QuoteTime { get; set; }
    
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; }
    
    [JsonPropertyName("totalVolume")]
    public decimal? TotalVolume { get; set; }
    
    [JsonPropertyName("tradeTime")]
    public decimal? TradeTime { get; set; }
  }
}
