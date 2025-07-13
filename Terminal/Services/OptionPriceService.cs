namespace Terminal.Services
{
  using QuantLib;

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

    public double ComputeDelta(Option.Type optionType, double? spotPrice, double? strikePrice, double timeToMaturity)
    {
      spotQuote.setValue(spotPrice.Value);

      var maturityDate = evaluationDate + new Period((int)(timeToMaturity * 365), TimeUnit.Days);
      var exercise = new EuropeanExercise(maturityDate);
      var premium = new PlainVanillaPayoff(optionType, strikePrice.Value);
      var option = new VanillaOption(premium, exercise);

      option.setPricingEngine(engine);

      return option.delta();
    }

    public double ComputeSigma(double optionPrice, double spotPrice, double strikePrice, double timeToMaturity, Option.Type optionType)
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
  }
}
