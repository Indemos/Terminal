using Terminal.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Ib = InteractiveBrokers;
using Sc = Schwab;
using Scm = Schwab.Messages;
using Sim = Simulation;

namespace Terminal.Pages
{
  public partial class Options
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual InstrumentModel Instrument { get; set; } = new InstrumentModel
    {
      Name = "SPY",
      Type = InstrumentTypeEnum.Shares
    };

    /// <summary>
    /// Setup views and adapters
    /// </summary>
    /// <param name="setup"></param>
    /// <returns></returns>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        View.OnPreConnect = () =>
        {
          View.Adapters["Sim"] = CreateSimAccount();
          //View.Adapters["Ib"] = CreateIbAccount();
          //View.Adapters["Sc"] = CreateScAccount();
        };

        View.OnPostConnect = () =>
        {
          var order = new OrderModel
          {
            Transaction = new TransactionModel
            {
              Instrument = Instrument,
            }
          };

          //var interval = new Timer();

          //interval.Elapsed += async (o, e) => await OnData();
          //interval.Interval = 5000;
          //interval.Enabled = true;
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// Setup Schwab account
    /// </summary>
    /// <returns></returns>
    protected virtual Sc.Adapter CreateScAccount()
    {
      var account = new Account
      {
        Descriptor = Configuration["Schwab:Account"],
        Instruments = new Dictionary<string, InstrumentModel>
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
    protected virtual Ib.Adapter CreateIbAccount()
    {
      var account = new Account
      {
        Descriptor = Configuration["InteractiveBrokers:Account"],
        Instruments = new Dictionary<string, InstrumentModel>
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
    /// Setup simulation account
    /// </summary>
    /// <returns></returns>
    protected virtual Sim.Adapter CreateSimAccount()
    {
      var account = new Account
      {
        Balance = 25000,
        Instruments = new Dictionary<string, InstrumentModel>
        {
          [Instrument.Name] = Instrument
        }
      };

      account
        .Instruments
        .Values
        .ForEach(o => o.Points.CollectionChanged += (_, e) => e
          .NewItems
          .OfType<PointModel>()
          .ForEach(async o => await OnData(o)));

      return new Sim.Adapter
      {
        Account = account,
        Source = Configuration["Simulation:Source"]
      };
    }

    /// <summary>
    /// Process tick data
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    private async Task OnData(PointModel point)
    {
      var simAdapter = View.Adapters["Sim"];
      var account = simAdapter.Account;

      if (account.ActiveOrders.Count == 0 && account.ActivePositions.Count == 0)
      {
        var options = await GetOptions(point);
        var orders = GetShortStraddle(point, options);
        var orderResponse = await simAdapter.CreateOrders(orders);
      }

      if (account.ActivePositions.Count > 1)
      {
      }
    }

    /// <summary>
    /// Get option chain
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected virtual async Task<IList<InstrumentModel>> GetOptions(PointModel point)
    {
      var simAdapter = View.Adapters["Sim"];
      var account = simAdapter.Account;
      var optionArgs = new OptionScreenerModel
      {
        Name = Instrument.Name,
        MinDate = DateTime.Now,
        MaxDate = DateTime.Now.AddDays(3),
        Point = point
      };

      var options = await simAdapter.GetOptions(optionArgs, []);
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
    /// Create short straddle strategy
    /// </summary>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected virtual OrderModel[] GetShortStraddle(PointModel point, IList<InstrumentModel> options)
    {
      var shortPut = options
        .Where(o => Equals(o.Derivative.Side, OptionSideEnum.Put))
        .Where(o => o.Derivative.Strike >= point.Last)
        .FirstOrDefault();

      var shortCall = options
        .Where(o => Equals(o.Derivative.Side, OptionSideEnum.Call))
        .Where(o => o.Derivative.Strike >= point.Last)
        .FirstOrDefault();

      var order = new OrderModel
      {
        Orders =
        [
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Type = OrderTypeEnum.Limit,
            Instruction = InstructionEnum.Side,
            Price = shortPut.Point.Bid,
            Transaction = new()
            {
              Volume = 1,
              Instrument = shortPut
            }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Type = OrderTypeEnum.Limit,
            Instruction = InstructionEnum.Side,
            Price = shortCall.Point.Bid,
            Transaction = new()
            {
              Volume = 1,
              Instrument = shortCall
            }
          }
        ]
      };

      return [order];
    }
  }
}
