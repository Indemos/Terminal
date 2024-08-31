using Terminal.Components;
using Distribution.Services;
using Distribution.Stream;
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
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Terminal.Core.Services;

namespace Terminal.Pages
{
  public partial class Recorder
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual Service Srv { get; set; } = new Service();
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

      var interval = new Timer();

      interval.Elapsed += async (o, e) => await OnData();
      interval.Interval = 5000;
      interval.Enabled = true;
    }

    protected async Task OnData()
    {
      try
      {
        var optionArgs = new OptionScreenerModel
        {
          Name = "SPY",
          Count = 100,
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

        message.Derivatives = new Dictionary<string, IList<InstrumentModel>>
        {
          [nameof(InstrumentEnum.Options)] = options.Data
        };

        Directory.CreateDirectory(location);

        var content = JsonSerializer.Serialize(message, Srv.Options);
        var source = $"{location}/{DateTime.UtcNow.Ticks}.zip";

        using var archive = ZipFile.Open(source, ZipArchiveMode.Create);
        using (var entry = archive.CreateEntry($"{DateTime.UtcNow.Ticks}").Open())
        {
          var bytes = Encoding.ASCII.GetBytes(content);
          entry.Write(bytes);
        }
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
    }
  }
}
