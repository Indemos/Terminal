namespace Dashboard.Services
{
  using Core.Enums;
  using Estimator.Services;
  using QuantLib;
  using System;

  public class OptionPriceService
  {
    private readonly Settings settings = Settings.instance();
    private readonly Date evaluationDate = Date.todaysDate();
    private readonly DayCounter dayCounter = new Actual365Fixed();

    private readonly SimpleQuote spotQuote;
    private readonly SimpleQuote rateQuote;
    private readonly SimpleQuote dividendQuote;
    private readonly SimpleQuote volQuote;

    private readonly BlackScholesMertonProcess process;
    private readonly AnalyticEuropeanEngine engine;

    private readonly YieldTermStructureHandle rateHandle;
    private readonly YieldTermStructureHandle dividendHandle;
    private readonly BlackVolTermStructureHandle volHandle;

    private readonly OptionService optionService = new();

    public OptionPriceService(double riskFreeRate, double dividendRate, double volatility)
    {
      settings.setEvaluationDate(evaluationDate);

      spotQuote = new SimpleQuote();
      rateQuote = new SimpleQuote(riskFreeRate);
      dividendQuote = new SimpleQuote(dividendRate);
      volQuote = new SimpleQuote(volatility);

      rateHandle = new YieldTermStructureHandle(new FlatForward(evaluationDate, new QuoteHandle(rateQuote), dayCounter));
      dividendHandle = new YieldTermStructureHandle(new FlatForward(evaluationDate, new QuoteHandle(dividendQuote), dayCounter));
      volHandle = new BlackVolTermStructureHandle(new BlackConstantVol(evaluationDate, new NullCalendar(), new QuoteHandle(volQuote), dayCounter));

      process = new BlackScholesMertonProcess(
        new QuoteHandle(spotQuote),
        dividendHandle,
        rateHandle,
        volHandle);

      engine = new AnalyticEuropeanEngine(process);
    }

    /// <summary>
    /// Estimated delta
    /// </summary>
    /// <param name="optionType"></param>
    /// <param name="spotPrice"></param>
    /// <param name="strikePrice"></param>
    /// <param name="timeToMaturity"></param>
    public double Delta(
      string optionType,
      double? spotPrice,
      double? strikePrice,
      double timeToMaturity,
      double volatility,
      double riskRate = 0.05,
      double divRate = 0.05) => OptionService.Delta(
        optionType,
        spotPrice.Value,
        strikePrice.Value,
        timeToMaturity,
        volatility,
        riskRate,
        divRate);

    /// <summary>
    /// Estimated volatility
    /// </summary>
    /// <param name="optionPrice"></param>
    /// <param name="spotPrice"></param>
    /// <param name="strikePrice"></param>
    /// <param name="timeToMaturity"></param>
    /// <param name="optionType"></param>
    public double Sigma(double optionPrice, double spotPrice, double strikePrice, double timeToMaturity, Option.Type optionType)
    {
      spotQuote.setValue(spotPrice);

      var maturityDate = evaluationDate + new Period((int)(timeToMaturity * 365), TimeUnit.Days);
      var exercise = new EuropeanExercise(maturityDate);
      var premium = new PlainVanillaPayoff(optionType, strikePrice);
      var option = new VanillaOption(premium, exercise);

      option.setPricingEngine(engine);

      return option.impliedVolatility(
          optionPrice,
          process,
          1e-6,   // accuracy
          1000,   // max iterations
          1e-4,   // min vol guess
          5.0     // max vol guess
      );
    }

    /// <summary>
    /// Estimated price
    /// </summary>
    /// <param name="optionType"></param>
    /// <param name="spotPrice"></param>
    /// <param name="strikePrice"></param>
    /// <param name="timeToMaturity"></param>
    /// <param name="volatility"></param>
    /// <param name="riskRate"></param>
    /// <param name="divRate"></param>
    public double Price(
      string optionType,
      double spotPrice,
      double strikePrice,
      double timeToMaturity,
      double volatility,
      double riskRate = 0.05,
      double divRate = 0.05) => OptionService.Price(
        optionType,
        spotPrice,
        strikePrice,
        timeToMaturity,
        volatility,
        riskRate,
        divRate);
  }
}
