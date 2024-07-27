using Client.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Ib = InteractiveBrokers;
using Sc = Schwab;
using Scm = Schwab.Messages;

namespace Client.Pages
{
  public partial class Options
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual Ib.Adapter IbAdapter { get; set; }
    protected virtual Sc.Adapter ScAdapter { get; set; }
    protected virtual InstrumentModel Instrument { get; set; } = new InstrumentModel
    {
      Name = "F",
      Security = "STK"
    };

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        View.OnPreConnect = () =>
        {
          View.Adapters["Ib"] = IbAdapter = CreateIbAccount();
          //View.Adapters["Sc"] = ScAdapter = CreateScAccount();
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

          //await IbAdapter.CreateOrders(order);

          //await OnData();

          //var interval = new Timer();

          //interval.Elapsed += async (o, e) => await OnData();
          //interval.Interval = 5000;
          //interval.Enabled = true;
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

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

    private async Task OnData()
    {
      var optionArgs = new OptionScreenerModel
      {
        Name = Instrument.Name,
        MinDate = DateTime.Now,
        MaxDate = DateTime.Now.AddDays(1)
      };

      var domArgs = new DomScreenerModel
      {
        Name = Instrument.Name
      };

      var dom = await ScAdapter.GetDom(domArgs, []);
      var options = await ScAdapter.GetOptions(optionArgs, []);
      var price = dom.Data.Asks.First().Last;
    }
  }
}
