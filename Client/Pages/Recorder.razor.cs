using Client.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Schwab;
using Schwab.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Terminal.Core.Domains;
using Terminal.Core.Extensions;
using Terminal.Core.Indicators;
using Terminal.Core.Models;

namespace Client.Pages
{
  public partial class Recorder
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual IGateway Adapter
    {
      get => View.Adapters.Get("Demo");
      set => View.Adapters["Demo"] = value;
    }

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
      var account = new Account
      {
        Descriptor = Configuration["Schwab:Account"],
        Instruments = new Dictionary<string, InstrumentModel>
        {
          ["SPY"] = new InstrumentModel { Name = "SPY" }
        }
      };

      Adapter = new Adapter
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

      //var interval = new Timer();

      //interval.Elapsed += async (o, e) => await OnData();
      //interval.Interval = 5000;
      //interval.Enabled = true;
    }

    private async Task OnData()
    {
      var optionArgs = new OptionScreenerModel
      {
        Name = "SPY",
        MinDate = DateTime.Now,
        MaxDate = DateTime.Now.AddYears(1)
      };

      var domArgs = new DomScreenerModel
      {
        Name = "SPY"
      };

      var dom = await Adapter.GetDom(domArgs, []);
      var options = await Adapter.GetOptions(optionArgs, []);
      var message = dom.Data.Bids.First();
      var location = $"D:/Code/NET/Terminal/Data/SPY/{DateTime.Now:yyyy-MM-dd}";

      message.Derivatives = new Dictionary<string, IList<DerivativeModel>>
      {
        ["Options"] = options.Data
      };

      Directory.CreateDirectory(location);

      var content = JsonSerializer.Serialize(message);
      var source = $"{location}/{DateTime.UtcNow.Ticks}.zip";

      using var archive = ZipFile.Open(source, ZipArchiveMode.Create);
      using (var entry = archive.CreateEntry($"{DateTime.UtcNow.Ticks}").Open())
      {
        var bytes = Encoding.ASCII.GetBytes(content);
        entry.Write(bytes);
      }
    }
  }
}
