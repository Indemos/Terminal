using Client.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Schwab;
using Schwab.Messages;
using Simulation.Messages;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Indicators;
using Terminal.Core.Models;

namespace Client.Pages
{
  public partial class Options
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual Adapter SchwabAdapter { get; set; }
    protected virtual InteractiveBrokers.Adapter IbAdapter { get; set; }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        View.OnPreConnect = () =>
        {
          IbAdapter = CreateIbAccount();
          SchwabAdapter = CreateSchwabAccount();
        };

        View.OnPostConnect = async () => await OnData();
      }

      await base.OnAfterRenderAsync(setup);
    }

    protected virtual Adapter CreateSchwabAccount()
    {
      var account = new Account
      {
        Descriptor = Configuration["Schwab:Account"],
        Instruments = new Dictionary<string, InstrumentModel>
        {
          ["F"] = new InstrumentModel { Name = "F" }
        }
      };

      return new Adapter
      {
        Account = account,
        Scope = new ScopeMessage
        {
          AccessToken = Configuration["Schwab:AccessToken"],
          RefreshToken = Configuration["Schwab:RefreshToken"],
          ConsumerKey = Configuration["Schwab:ConsumerKey"],
          ConsumerSecret = Configuration["Schwab:ConsumerSecret"],
        }
      };
    }

    protected virtual InteractiveBrokers.Adapter CreateIbAccount()
    {
      var account = new Account
      {
        Descriptor = Configuration["InteractiveBrokers:Account"],
        Instruments = new Dictionary<string, InstrumentModel>
        {
          ["F"] = new InstrumentModel { Name = "F" }
        }
      };

      return new InteractiveBrokers.Adapter
      {
        Account = account
      };
    }

    private async Task OnData()
    {
      var optionArgs = new OptionsArgs
      {
        Name = "SPY",
        MinDate = DateTime.Now,
        MaxDate = DateTime.Now.AddYears(1)
      };

      var domArgs = new DomArgs
      {
        Name = "SPY"
      };

      var dom = await View.Adapter.GetDom(domArgs, []);
      var options = await View.Adapter.GetOptions(optionArgs, []);
      var message = new PointMessage
      {
        Point = dom.Data.Bids.First(),
        Options = options.Data
      };
      var content = JsonSerializer.Serialize(message);
      var source = $"D:/Code/NET/Terminal/Data/SPY/{DateTime.UtcNow.Ticks}.zip";

      using var archive = ZipFile.Open(source, ZipArchiveMode.Create);
      using (var entry = archive.CreateEntry($"{DateTime.UtcNow.Ticks}").Open())
      {
        var bytes = Encoding.ASCII.GetBytes(content);
        entry.Write(bytes);
      }
    }
  }
}
