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
    public double? Bid { get; set; }

    [JsonPropertyName("ask")]
    public double? Ask { get; set; }

    [JsonPropertyName("last")]
    public double? Last { get; set; }

    [JsonPropertyName("mark")]
    public double? Mark { get; set; }

    [JsonPropertyName("bidSize")]
    public double? BidSize { get; set; }

    [JsonPropertyName("askSize")]
    public double? AskSize { get; set; }

    [JsonPropertyName("bidAskSize")]
    public string BidAskSize { get; set; }

    [JsonPropertyName("lastSize")]
    public double? LastSize { get; set; }

    [JsonPropertyName("highPrice")]
    public double? HighPrice { get; set; }

    [JsonPropertyName("lowPrice")]
    public double? LowPrice { get; set; }

    [JsonPropertyName("openPrice")]
    public double? OpenPrice { get; set; }

    [JsonPropertyName("closePrice")]
    public double? ClosePrice { get; set; }

    [JsonPropertyName("totalVolume")]
    public double? TotalVolume { get; set; }

    [JsonPropertyName("tradeDate")]
    public DateTime TradeDate { get; set; }

    [JsonPropertyName("quoteTimeInLong")]
    public long? QuoteTimeInLong { get; set; }

    [JsonPropertyName("tradeTimeInLong")]
    public long? TradeTimeInLong { get; set; }

    [JsonPropertyName("netChange")]
    public double? NetChange { get; set; }

    [JsonPropertyName("volatility")]
    public double? Volatility { get; set; }

    [JsonPropertyName("delta")]
    public double? Delta { get; set; }

    [JsonPropertyName("gamma")]
    public double? Gamma { get; set; }

    [JsonPropertyName("theta")]
    public double? Theta { get; set; }

    [JsonPropertyName("vega")]
    public double? Vega { get; set; }

    [JsonPropertyName("rho")]
    public double? Rho { get; set; }

    [JsonPropertyName("timeValue")]
    public double? TimeValue { get; set; }

    [JsonPropertyName("openInterest")]
    public double? OpenInterest { get; set; }

    [JsonPropertyName("inTheMoney")]
    public bool? IsInTheMoney { get; set; }

    [JsonPropertyName("theoreticalOptionValue")]
    public double? TheoreticalOptionValue { get; set; }

    [JsonPropertyName("theoreticalVolatility")]
    public double? TheoreticalVolatility { get; set; }

    [JsonPropertyName("mini")]
    public bool? IsMini { get; set; }

    [JsonPropertyName("nonStandard")]
    public bool? IsNonStandard { get; set; }

    [JsonPropertyName("optionDeliverablesList")]
    public List<OptionDeliverables> OptionDeliverablesList { get; set; }

    [JsonPropertyName("strikePrice")]
    public double? StrikePrice { get; set; }

    [JsonPropertyName("lastTradingDay")]
    public double? LastTradingDay { get; set; }

    [JsonPropertyName("expirationDate")]
    public long? ExpirationDate { get; set; }

    [JsonPropertyName("expirationType")]
    public string ExpirationType { get; set; }

    [JsonPropertyName("multiplier")]
    public double? Multiplier { get; set; }

    [JsonPropertyName("intrinsicValue")]
    public double? IntrinsicValue { get; set; }

    [JsonPropertyName("settlementType")]
    public string SettlementType { get; set; }

    [JsonPropertyName("deliverableNote")]
    public string DeliverableNote { get; set; }

    [JsonPropertyName("isIndexOption")]
    public bool? IsIndexOption { get; set; }

    [JsonPropertyName("pennyPilot")]
    public bool? IsPennyPilot { get; set; }

    [JsonPropertyName("percentChange")]
    public double? PercentChange { get; set; }

    [JsonPropertyName("markChange")]
    public double? MarkChange { get; set; }

    [JsonPropertyName("markPercentChange")]
    public double? MarkPercentChange { get; set; }
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
    public double? Interval { get; set; }
    
    [JsonPropertyName("isDelayed")]
    public bool? IsDelayed { get; set; }
    
    [JsonPropertyName("isIndex")]
    public bool? IsIndex { get; set; }
    
    [JsonPropertyName("daysToExpiration")]
    public double? DaysToExpiration { get; set; }

    [JsonPropertyName("interestRate")]
    public double? InterestRate { get; set; }
    
    [JsonPropertyName("underlyingPrice")]
    public double? UnderlyingPrice { get; set; }
    
    [JsonPropertyName("volatility")]
    public double? Volatility { get; set; }
    
    [JsonPropertyName("callExpDateMap")]
    public Dictionary<string, Dictionary<string, List<Option>>> CallExpDateMap { get; set; }
    
    [JsonPropertyName("putExpDateMap")]
    public Dictionary<string, Dictionary<string, List<Option>>> PutExpDateMap { get; set; }
  }

  public struct Underlying
  {    
    [JsonPropertyName("ask")]
    public double? Ask { get; set; }
    
    [JsonPropertyName("askSize")]
    public double? AskSize { get; set; }
    
    [JsonPropertyName("bid")]
    public double? Bid { get; set; }
    
    [JsonPropertyName("bidSize")]
    public double? BidSize { get; set; }
    
    [JsonPropertyName("change")]
    public double? Change { get; set; }
    
    [JsonPropertyName("close")]
    public double? Close { get; set; }
    
    [JsonPropertyName("delayed")]
    public bool? Delayed { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("exchangeName")]
    public string ExchangeName { get; set; }
    
    [JsonPropertyName("fiftyTwoWeekHigh")]
    public double? FiftyTwoWeekHigh { get; set; }
    
    [JsonPropertyName("fiftyTwoWeekLow")]
    public double? FiftyTwoWeekLow { get; set; }
    
    [JsonPropertyName("highPrice")]
    public double? HighPrice { get; set; }
    
    [JsonPropertyName("last")]
    public double? Last { get; set; }
    
    [JsonPropertyName("lowPrice")]
    public double? LowPrice { get; set; }
    
    [JsonPropertyName("mark")]
    public double? Mark { get; set; }
    
    [JsonPropertyName("markChange")]
    public double? MarkChange { get; set; }
    
    [JsonPropertyName("markPercentChange")]
    public double? MarkPercentChange { get; set; }
    
    [JsonPropertyName("openPrice")]
    public double? OpenPrice { get; set; }
    
    [JsonPropertyName("percentChange")]
    public double? PercentChange { get; set; }
    
    [JsonPropertyName("quoteTime")]
    public double? QuoteTime { get; set; }
    
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; }
    
    [JsonPropertyName("totalVolume")]
    public double? TotalVolume { get; set; }
    
    [JsonPropertyName("tradeTime")]
    public double? TradeTime { get; set; }
  }
}
