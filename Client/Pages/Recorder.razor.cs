using Client.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Schwab;
using Schwab.Messages;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Terminal.Core.Domains;
using Terminal.Core.Indicators;
using Terminal.Core.Models;

namespace Client.Pages
{
  public partial class Recorder
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual IAccount Account { get; set; }
    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        View.OnPreConnect = CreateAccounts;
        View.OnPostConnect = async () => await OnData();
      }

      await base.OnAfterRenderAsync(setup);
    }

    protected virtual void CreateAccounts()
    {
      Account = new Account
      {
        Descriptor = Configuration["Schwab:Account"],
        Instruments = new Dictionary<string, InstrumentModel>
        {
          ["SPY"] = new InstrumentModel { Name = "SPY" }
        }
      };

      View.Adapter = new Adapter
      {
        Account = Account,
        Scope = new ScopeMessage
        {
          AccessToken = Configuration["Schwab:AccessToken"],
          RefreshToken = Configuration["Schwab:RefreshToken"],
          ConsumerKey = Configuration["Schwab:ConsumerKey"],
          ConsumerSecret = Configuration["Schwab:ConsumerSecret"],
        }
      };

      var aTimer = new Timer();
      aTimer.Elapsed += async (o, e) => await OnData();
      aTimer.Interval = 5000;
      aTimer.Enabled = true;
    }

    private async Task OnData()
    {
      var optionArgs = new OptionScreenModel
      {
        Name = "SPY",
        MinDate = DateTime.Now,
        MaxDate = DateTime.Now.AddYears(1)
      };

      var domArgs = new DomScreenModel
      {
        Name = "SPY"
      };

      var dom = await View.Adapter.GetDom(domArgs, []);
      var options = await View.Adapter.GetOptions(optionArgs, []);
      var message = new SnapshotModel
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
