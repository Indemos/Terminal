using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Terminal.Core.Services;
using Terminal.Models;
using static MudBlazor.Colors;
using Ib = InteractiveBrokers;
using Sc = Schwab;
using Scm = Schwab.Messages;
using Sim = Simulation;

namespace Terminal.Components
{
  public partial class OptionPageComponent
  {
    [Inject] IConfiguration Configuration { get; set; }

    public virtual PageComponent View { get; set; }
    public virtual ChartsComponent FramesView { get; set; }
    public virtual ChartsComponent StrikesView { get; set; }
    public virtual ChartsComponent PremiumsView { get; set; }
    public virtual ChartsComponent PositionsView { get; set; }
    public virtual PerformanceIndicator Performance { get; set; }
    public virtual InstrumentModel Instrument { get; set; }

    /// <summary>
    /// Setup views and adapters
    /// </summary>
    /// <param name="action"></param>
    /// <param name="span"></param>
    /// <returns></returns>
    public virtual async Task OnLoad(Func<PointModel, Task> action, IList<Shape> groups = null)
    {
      await CreateViews(groups);

      Performance = new PerformanceIndicator { Name = nameof(Performance) };

      View.OnPreConnect = () =>
      {
        View.Adapters["Sim"] = CreateSimAccount();
      };

      View.OnPostConnect = () =>
      {
        var account = View.Adapters["Sim"].Account;

        View.DealsView.UpdateItems(account.Deals);
        View.OrdersView.UpdateItems(account.Orders.Values);
        View.PositionsView.UpdateItems(account.Positions.Values);

        account
          .Instruments
          .Values
          .ForEach(o => o.PointGroups.CollectionChanged += (_, e) => e
            .NewItems
            .OfType<PointModel>()
            .ForEach(async o => await action(o)));
      };
    }

    /// <summary>
    /// Charts setup
    /// </summary>
    /// <returns></returns>
    public virtual async Task CreateViews(IList<Shape> groups)
    {
      // Indicators

      var priceAreas = new Shape();
      var priceCharts = Enumerable.Range(0, 7).Select(o => new Shape()).ToList();

      priceCharts[0].Groups["PutDelta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      priceCharts[0].Groups["CallDelta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      priceCharts[1].Groups["PutGamma"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      priceCharts[1].Groups["CallGamma"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      priceCharts[2].Groups["PutVega"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      priceCharts[2].Groups["CallVega"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      priceCharts[3].Groups["PutTheta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      priceCharts[3].Groups["CallTheta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      priceCharts[4].Groups["PutBids"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      priceCharts[4].Groups["PutAsks"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      priceCharts[5].Groups["CallBids"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      priceCharts[5].Groups["CallAsks"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      priceCharts[6].Groups["PutRatio"] = new LineShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      priceCharts[6].Groups["CallRatio"] = new LineShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      priceCharts[6].Groups["PcRatio"] = new LineShape { Component = new ComponentModel { Color = SKColors.LimeGreen } };

      priceCharts = [.. (groups ?? []), .. priceCharts];

      for (var i = 0; i < priceCharts.Count; i++)
      {
        priceAreas.Groups[$"{i}"] = priceCharts[i];
      }

      await View.ChartsView.Create(priceAreas);

      // Position metrics

      var positionAreas = new Shape();
      var positionCharts = Enumerable.Range(0, 6).Select(o => new Shape()).ToList();

      positionCharts[0].Groups["BasisDelta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      positionCharts[0].Groups["OptionDelta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      positionCharts[1].Groups["LongDelta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      positionCharts[1].Groups["ShortDelta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      positionCharts[2].Groups["LongGamma"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      positionCharts[2].Groups["ShortGamma"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      positionCharts[3].Groups["LongVega"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      positionCharts[3].Groups["ShortVega"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      positionCharts[4].Groups["LongIv"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      positionCharts[4].Groups["ShortIv"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      positionCharts[5].Groups["LongTheta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      positionCharts[5].Groups["ShortTheta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };

      for (var i = 0; i < positionCharts.Count; i++)
      {
        positionAreas.Groups[$"{i}"] = positionCharts[i];
      }

      await PositionsView.Create(positionAreas);

      // Balance

      var balanceAreas = new Shape();
      var balanceCharts = Enumerable.Range(0, 1).Select(o => new Shape()).ToList();

      balanceCharts[0].Groups["Balance"] = new AreaShape { Component = new ComponentModel { Color = SKColors.Black } };
      balanceCharts[0].Groups["PnL"] = new LineShape { Component = new ComponentModel { Color = SKColors.OrangeRed, Size = 2 } };

      for (var i = 0; i < balanceCharts.Count; i++)
      {
        balanceAreas.Groups[$"{i}"] = balanceCharts[i];
      }

      await View.ReportsView.Create(balanceAreas);

      // Frame

      var spanAreas = new Shape();
      var spanCharts = Enumerable.Range(0, 1).Select(o => new Shape()).ToList();

      spanCharts[0].Groups["Bars"] = new CandleShape();

      for (var i = 0; i < spanCharts.Count; i++)
      {
        spanAreas.Groups[$"{i}"] = spanCharts[i];
      }

      await FramesView.Create(spanAreas);

      // Premium

      var premiumAreas = new Shape();
      var premiumCharts = Enumerable.Range(0, 1).Select(o => new Shape()).ToList();

      premiumCharts[0].Groups["Estimate"] = new LineShape { Component = new ComponentModel { Color = SKColors.LimeGreen, Size = 2 } };

      for (var i = 0; i < premiumCharts.Count; i++)
      {
        premiumAreas.Groups[$"{i}"] = premiumCharts[i];
      }

      await PremiumsView.Create(premiumAreas);

      // Strikes

      var strikeAreas = new Shape();
      var strikeCharts = Enumerable.Range(0, 1).Select(o => new Shape()).ToList();

      strikeCharts[0].Groups["Gamma"] = new LineShape { Component = new ComponentModel { Color = SKColors.LimeGreen, Size = 2 } };

      for (var i = 0; i < strikeCharts.Count; i++)
      {
        strikeAreas.Groups[$"{i}"] = strikeCharts[i];
      }

      await StrikesView.Create(strikeAreas);
    }

    /// <summary>
    /// Setup simulation account
    /// </summary>
    /// <returns></returns>
    public virtual Sim.Adapter CreateSimAccount()
    {
      var account = new Account
      {
        Balance = 25000,
        Instruments = new ConcurrentDictionary<string, InstrumentModel>
        {
          [Instrument.Name] = Instrument
        }
      };

      return new Sim.Adapter
      {
        Account = account,
        Source = Configuration["Simulation:Source"]
      };
    }

    /// <summary>
    /// Setup Schwab account
    /// </summary>
    /// <returns></returns>
    public virtual Sc.Adapter CreateScAccount()
    {
      var account = new Account
      {
        Descriptor = Configuration["Schwab:Account"],
        Instruments = new ConcurrentDictionary<string, InstrumentModel>
        {
          [Instrument.Name] = Instrument
        }
      };

      return new Sc.Adapter
      {
        Account = account,
        Scope = new Scm.ScopeMessage
        {
          AccessToken = Configuration["Schwab:AccessToken"],
          RefreshToken = Configuration["Schwab:RefreshToken"],
          ConsumerKey = Configuration["Schwab:ConsumerKey"],
          ConsumerSecret = Configuration["Schwab:ConsumerSecret"],
        }
      };
    }

    /// <summary>
    /// Setup Interactive Brokers account
    /// </summary>
    /// <returns></returns>
    public virtual Ib.Adapter CreateIbAccount()
    {
      var account = new Account
      {
        Descriptor = Configuration["InteractiveBrokers:Account"],
        Instruments = new ConcurrentDictionary<string, InstrumentModel>
        {
          [Instrument.Name] = Instrument
        }
      };

      return new Ib.Adapter
      {
        Account = account
      };
    }

    /// <summary>
    /// Get position delta
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public virtual double GetDelta(OrderModel o)
    {
      var volume = o.Transaction?.Volume;
      var leverage = o.Transaction?.Instrument?.Leverage;
      var delta = o.Transaction?.Instrument?.Derivative?.Variable?.Delta;
      var side = o.Side is OrderSideEnum.Buy ? 1.0 : -1.0;

      return ((delta ?? volume) * leverage * side) ?? 0;
    }

    /// <summary>
    /// Process tick data
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual async Task OnUpdate(PointModel point, Action<IList<InstrumentModel>> action)
    {
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
      var options = await GetOptions(point);

      action([.. options]);

      var puts = options.Where(o => o.Derivative.Side is OptionSideEnum.Put);
      var calls = options.Where(o => o.Derivative.Side is OptionSideEnum.Call);
      var putBids = puts.Sum(o => o.Point.BidSize ?? 0);
      var putAsks = puts.Sum(o => o.Point.AskSize ?? 0);
      var callBids = calls.Sum(o => o.Point.BidSize ?? 0);
      var callAsks = calls.Sum(o => o.Point.AskSize ?? 0);

      FramesView.UpdateItems([
        KeyValuePair.Create("Bars", point)
      ]);

      View.ChartsView.UpdateItems([
        KeyValuePair.Create("PutDelta", new PointModel { Time = point.Time, Last = puts.Sum(o => o.Derivative.Variable.Delta) }),
        KeyValuePair.Create("CallDelta", new PointModel { Time = point.Time, Last = calls.Sum(o => o.Derivative.Variable.Delta) }),
        KeyValuePair.Create("PutVega", new PointModel { Time = point.Time, Last = -puts.Sum(o => o.Derivative.Variable.Vega) }),
        KeyValuePair.Create("CallVega", new PointModel { Time = point.Time, Last = calls.Sum(o => o.Derivative.Variable.Vega) }),
        KeyValuePair.Create("PutGamma", new PointModel { Time = point.Time, Last = -puts.Sum(o => o.Derivative.Variable.Gamma) }),
        KeyValuePair.Create("CallGamma", new PointModel { Time = point.Time, Last = calls.Sum(o => o.Derivative.Variable.Gamma) }),
        KeyValuePair.Create("PutTheta", new PointModel { Time = point.Time, Last = -puts.Sum(o => o.Derivative.Variable.Theta) }),
        KeyValuePair.Create("CallTheta", new PointModel { Time = point.Time, Last = calls.Sum(o => o.Derivative.Variable.Theta) }),
        KeyValuePair.Create("PutBids", new PointModel { Time = point.Time, Last = putBids }),
        KeyValuePair.Create("PutAsks", new PointModel { Time = point.Time, Last = -putAsks }),
        KeyValuePair.Create("PutRatio", new PointModel { Time = point.Time, Last = putBids - putAsks }),
        KeyValuePair.Create("CallBids", new PointModel { Time = point.Time, Last = callBids }),
        KeyValuePair.Create("CallAsks", new PointModel { Time = point.Time, Last = -callAsks }),
        KeyValuePair.Create("CallRatio", new PointModel { Time = point.Time, Last = callBids - callAsks }),
        KeyValuePair.Create("PcRatio", new PointModel { Time = point.Time, Last = (callBids - callAsks) - (putBids - putAsks) })
      ]);

      var performance = Performance.Calculate([account]);

      View.ReportsView.UpdateItems([
        KeyValuePair.Create("Balance", new PointModel { Time = point.Time, Last = account.Balance }),
        KeyValuePair.Create("PnL", new PointModel { Time = point.Time, Last = performance.Point.Last })
      ]);

      var positions = account.Positions.Values;
      var basisPositions = positions.Where(o => o.Transaction.Instrument.Derivative is null);
      var optionPositions = positions.Where(o => o.Transaction.Instrument.Derivative is not null);
      var posPuts = optionPositions.Where(o => o.Transaction.Instrument.Derivative.Side is OptionSideEnum.Put);
      var posCalls = optionPositions.Where(o => o.Transaction.Instrument.Derivative.Side is OptionSideEnum.Call);

      var longs = basisPositions
        .Where(o => o.Side is OrderSideEnum.Buy)
        .Concat(posCalls.Where(o => o.Side is OrderSideEnum.Buy))
        .Concat(posPuts.Where(o => o.Side is OrderSideEnum.Sell));

      var shorts = basisPositions
        .Where(o => o.Side is OrderSideEnum.Sell)
        .Concat(posPuts.Where(o => o.Side is OrderSideEnum.Buy))
        .Concat(posCalls.Where(o => o.Side is OrderSideEnum.Sell));

      var x = optionPositions.Sum(GetDelta);

      PositionsView.UpdateItems([
        KeyValuePair.Create("OptionDelta", new PointModel { Time = point.Time, Last = optionPositions.Sum(GetDelta) }),
        KeyValuePair.Create("BasisDelta", new PointModel { Time = point.Time, Last = basisPositions.Sum(GetDelta) }),
        KeyValuePair.Create("LongDelta", new PointModel { Time = point.Time, Last = longs.Sum(GetDelta) }),
        KeyValuePair.Create("ShortDelta", new PointModel { Time = point.Time, Last = shorts.Sum(GetDelta) }),
        KeyValuePair.Create("LongGamma", new PointModel { Time = point.Time, Last = longs.Sum(o => o.Transaction.Instrument.Derivative?.Variable?.Gamma ?? 0) }),
        KeyValuePair.Create("ShortGamma", new PointModel { Time = point.Time, Last = -shorts.Sum(o => o.Transaction.Instrument.Derivative?.Variable?.Gamma ?? 0) }),
        KeyValuePair.Create("LongTheta", new PointModel { Time = point.Time, Last = longs.Sum(o => o.Transaction.Instrument.Derivative?.Variable?.Theta ?? 0) }),
        KeyValuePair.Create("ShortTheta", new PointModel { Time = point.Time, Last = -shorts.Sum(o => o.Transaction.Instrument.Derivative?.Variable?.Theta ?? 0) }),
        KeyValuePair.Create("LongVega", new PointModel { Time = point.Time, Last = longs.Sum(o => o.Transaction.Instrument.Derivative?.Variable?.Vega ?? 0) }),
        KeyValuePair.Create("ShortVega", new PointModel { Time = point.Time, Last = -shorts.Sum(o => o.Transaction.Instrument.Derivative?.Variable?.Vega ?? 0) }),
        KeyValuePair.Create("LongIv", new PointModel { Time = point.Time, Last = longs.Sum(o => o.Transaction.Instrument.Derivative?.Volatility ?? 0) }),
        KeyValuePair.Create("ShortIv", new PointModel { Time = point.Time, Last = -shorts.Sum(o => o.Transaction.Instrument.Derivative?.Volatility ?? 0) })
      ]);

      View.DealsView.UpdateItems(account.Deals);
      View.OrdersView.UpdateItems(account.Orders.Values);
      View.PositionsView.UpdateItems(account.Positions.Values);

      // Option estimate

      //PremiumsView.Clear();

      var sums = new Dictionary<double, double>();

      foreach (var pos in account.Positions.Values)
      {
        var plusPercents = Enumerable.Range(0, 20).Select((o, i) => o / 2.0 / 100.0);
        var minusPercents = Enumerable.Range(1, 20).Select((o, i) => -o / 2.0 / 100.0).Reverse();
        var inputModel = new OptionInputModel
        {
          Price = point.Last.Value,
          Amount = pos.Transaction.Volume ?? 0,
          Strike = pos.Transaction.Instrument?.Derivative?.Strike ?? 0,
          Premium = pos.Transaction.Instrument?.Point?.Last ?? 0,
          Date = pos.Transaction.Instrument?.Derivative?.Expiration,
          Side = pos.Transaction.Instrument?.Derivative?.Side ?? 0,
          Position = pos.Side.Value
        };

        var chartPoints = minusPercents.Concat(plusPercents).Select((o, i) =>
        {
          var step = inputModel.Price + inputModel.Price * o;
          var sum = GetEstimate(step, point.Time.Value, inputModel);
          sums[o] = sums.TryGetValue(o, out var s) ? s + sum : sum;
          return new LineShape { X = step, Y = sums[o] } as IShape;

        }).ToList();

        PremiumsView.UpdateItems("0", "Estimate", chartPoints);
      }
    }

    /// <summary>
    /// Estimated PnL for shares or options
    /// </summary>
    /// <param name="price"></param>
    /// <param name="inputModel"></param>
    /// <returns></returns>
    protected double GetEstimate(double price, DateTime date, OptionInputModel inputModel)
    {
      var direction = inputModel.Position is OrderSideEnum.Buy ? 1.0 : -1.0;

      if (inputModel.Side is not OptionSideEnum.Put && inputModel.Side is not OptionSideEnum.Call)
      {
        return (price - inputModel.Price) * inputModel.Amount * direction;
      }

      var days = Math.Max((inputModel.Date - date).Value.TotalDays / 250.0, double.Epsilon);
      var estimate = OptionService.Premium(inputModel.Side, price, inputModel.Strike, days, 0.25, 0.05, 0);

      return (estimate - inputModel.Premium) * inputModel.Amount * direction * 100;
    }

    /// <summary>
    /// Get option chain
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual async Task<IList<InstrumentModel>> GetOptions(PointModel point)
    {
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
      var optionArgs = new OptionScreenerModel
      {
        Name = Instrument.Name,
        MinDate = point.Time,
        MaxDate = point.Time.Value.AddDays(1),
        Point = point
      };

      var options = await adapter.GetOptions(optionArgs, []);
      var nextDayOptions = options
        .Data
        .OrderBy(o => o.Derivative.Expiration)
        .ThenBy(o => o.Derivative.Strike)
        .ThenBy(o => o.Derivative.Side)
        .GroupBy(o => o.Derivative.Expiration)
        .ToDictionary(o => o.Key, o => o.ToList())
        .FirstOrDefault()
        .Value;

      return nextDayOptions;
    }

    /// <summary>
    /// Close positions
    /// </summary>
    /// <param name="instrumentType"></param>
    /// <returns></returns>
    public async Task ClosePositions(InstrumentEnum? instrumentType = null)
    {
      var adapter = View.Adapters["Sim"];

      foreach (var position in adapter.Account.Positions.Values.ToList())
      {
        var order = new OrderModel
        {
          Side = position.Side is OrderSideEnum.Buy ? OrderSideEnum.Sell : OrderSideEnum.Buy,
          Type = OrderTypeEnum.Market,
          Transaction = new()
          {
            Volume = position.Transaction.Volume,
            Instrument = position.Transaction.Instrument
          }
        };

        var insEmpty = instrumentType is null;
        var insShares = instrumentType is InstrumentEnum.Shares && position.Transaction.Instrument.Derivative is null;
        var insOptions = instrumentType is InstrumentEnum.Options && position.Transaction.Instrument.Derivative is not null;

        if (insEmpty || insShares || insOptions)
        {
          await adapter.CreateOrders(order);
        }
      }
    }

    /// <summary>
    /// Create short straddle strategy
    /// </summary>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public virtual IList<OrderModel> GetCondor(PointModel point, IList<InstrumentModel> options)
    {
      var shortPut = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Put)
        .Where(o => o.Derivative.Strike >= point.Last)
        .FirstOrDefault();

      var longPut = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Put)
        .Where(o => o.Derivative.Strike < shortPut.Derivative.Strike - 3)
        .LastOrDefault();

      var shortCall = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Call)
        .Where(o => o.Derivative.Strike >= point.Last)
        .FirstOrDefault();

      var longCall = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Call)
        .Where(o => o.Derivative.Strike > shortCall.Derivative.Strike + 3)
        .FirstOrDefault();

      var order = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 1, Instrument = longPut }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 1, Instrument = longCall }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 1, Instrument = shortPut }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 1, Instrument = shortCall }
          }
        ]
      };

      return [order];
    }

    /// <summary>
    /// Create short straddle strategy
    /// </summary>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public virtual IList<OrderModel> GetShortStraddle(PointModel point, IList<InstrumentModel> options)
    {
      var shortPut = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Put)
        .Where(o => o.Derivative.Strike >= point.Last)
        .FirstOrDefault();

      var shortCall = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Call)
        .Where(o => o.Derivative.Strike >= point.Last)
        .FirstOrDefault();

      var order = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Instruction = InstructionEnum.Side,
            Price = shortPut.Point.Bid,
            Transaction = new() { Volume = 1, Instrument = shortPut }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Instruction = InstructionEnum.Side,
            Price = shortCall.Point.Bid,
            Transaction = new() { Volume = 1, Instrument = shortCall }
          }
        ]
      };

      return [order];
    }

    /// <summary>
    /// Hedge each delta change with shares
    /// </summary>
    /// <param name="price"></param>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual async Task<IList<OrderModel>> GetDirectionHedge(double price, PointModel point)
    {
      switch (true)
      {
        case true when price > 0 && point.Last > Math.Abs(price): 
        case true when price < 0 && point.Last < Math.Abs(price): await ClosePositions(InstrumentEnum.Shares); return [];
        case true when price > 0 && point.Last < Math.Abs(price): 
        case true when price < 0 && point.Last > Math.Abs(price): return GetShareHedge(point);
      }

      return [];
    }

    /// <summary>
    /// Hedge each delta change with shares
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual IList<OrderModel> GetShareHedge(PointModel point)
    {
      var account = View.Adapters["Sim"].Account;
      var basisDelta = Math.Round(account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is null)
        .Sum(GetDelta), MidpointRounding.ToZero);

      var optionDelta = Math.Round(account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is not null)
        .Sum(GetDelta), MidpointRounding.ToZero);

      var delta = optionDelta + basisDelta;

      if (Math.Abs(delta) > 0)
      {
        var order = new OrderModel
        {
          Type = OrderTypeEnum.Market,
          Side = delta < 0 ? OrderSideEnum.Buy : OrderSideEnum.Sell,
          Transaction = new() { Volume = Math.Abs(delta), Instrument = point.Instrument }
        };

        return [order];
      }

      return [];
    }

    /// <summary>
    /// Inverse share position to match option delta
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual IList<OrderModel> GetShareDirection(PointModel point)
    {
      var account = View.Adapters["Sim"].Account;
      var basisDelta = account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is null)
        .Sum(GetDelta);

      var optionDelta = account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is not null)
        .Sum(GetDelta);

      var isOversold = basisDelta < 0 && optionDelta > 0;
      var isOverbought = basisDelta > 0 && optionDelta < 0;

      if (basisDelta is 0 || isOversold || isOverbought)
      {
        var order = new OrderModel
        {
          Type = OrderTypeEnum.Market,
          Side = optionDelta > 0 ? OrderSideEnum.Buy : OrderSideEnum.Sell,
          Transaction = new() { Volume = 100, Instrument = point.Instrument }
        };

        return [order];
      }

      return [];
    }

    /// <summary>
    /// Create credit spread strategy
    /// </summary>
    /// <param name="side"></param>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public virtual IList<OrderModel> GetCreditSpread(Core.Enums.OptionSideEnum side, PointModel point, IList<InstrumentModel> options)
    {
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
      var position = account.Positions.FirstOrDefault().Value;
      var sideOptions = options.Where(o => Equals(o.Derivative.Side, side));
      var order = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        Orders =
        [
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Instruction = InstructionEnum.Side,
            Transaction = new TransactionModel { Volume = 1 }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new TransactionModel { Volume = 1 }
          }
        ]
      };

      switch (side)
      {
        case Core.Enums.OptionSideEnum.Put:

          var put = order.Orders[0].Transaction.Instrument = sideOptions
            .Where(o => o.Derivative.Strike <= GetPriceChange(point.Last, -0.001))
            .LastOrDefault();

          order.Orders[1].Transaction.Instrument = sideOptions
            .Where(o => o.Derivative.Strike <= GetPriceChange(point.Last, -0.005))
            .LastOrDefault();

          break;

        case Core.Enums.OptionSideEnum.Call:

          var call = order.Orders[0].Transaction.Instrument = sideOptions
            .Where(o => o.Derivative.Strike >= GetPriceChange(point.Last, 0.001))
            .FirstOrDefault();

          order.Orders[1].Transaction.Instrument = sideOptions
            .Where(o => o.Derivative.Strike >= GetPriceChange(point.Last, 0.005))
            .FirstOrDefault();

          break;
      }

      return [order];
    }

    /// <summary>
    /// Create debit spread strategy
    /// </summary>
    /// <param name="side"></param>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public virtual IList<OrderModel> GetDebigSpread(Core.Enums.OptionSideEnum side, PointModel point, IList<InstrumentModel> options)
    {
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
      var position = account.Positions.FirstOrDefault().Value;
      var sideOptions = options.Where(o => Equals(o.Derivative.Side, side));
      var order = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        Orders =
        [
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new TransactionModel { Volume = 1 }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Instruction = InstructionEnum.Side,
            Transaction = new TransactionModel { Volume = 1 }
          }
        ]
      };

      switch (side)
      {
        case Core.Enums.OptionSideEnum.Put:

          var put = order.Orders[0].Transaction.Instrument = sideOptions
            .Where(o => o.Derivative.Strike >= GetPriceChange(point.Last, 0.001))
            .FirstOrDefault();

          order.Orders[1].Transaction.Instrument = sideOptions
            .Where(o => o.Derivative.Strike <= GetPriceChange(point.Last, -0.005))
            .LastOrDefault();

          break;

        case Core.Enums.OptionSideEnum.Call:

          var call = order.Orders[0].Transaction.Instrument = sideOptions
            .Where(o => o.Derivative.Strike <= GetPriceChange(point.Last, -0.001))
            .LastOrDefault();

          order.Orders[1].Transaction.Instrument = sideOptions
            .Where(o => o.Derivative.Strike >= GetPriceChange(point.Last, 0.005))
            .FirstOrDefault();

          break;
      }

      return [order];
    }

    public double GetPriceChange(double? currentPrice, double? percentChange)
    {
      return (currentPrice + currentPrice * percentChange).Value;
    }
  }
}
