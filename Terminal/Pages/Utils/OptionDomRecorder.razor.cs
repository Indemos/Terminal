using Distribution.Services;
using Distribution.Stream;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Schwab;
using Schwab.Enums;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Terminal.Components;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;
using Terminal.Core.Services;
using Terminal.Services;

namespace Terminal.Pages.Utils
{
  public partial class OptionDomRecorder
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual ControlsComponent View { get; set; }
    protected virtual Service Srv { get; set; } = new Service();
    protected virtual InstrumentModel Instrument { get; set; } = new InstrumentModel { Name = "SPY" };

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        InstanceService<SubscriptionService>.Instance.OnUpdate += async state =>
        {
          switch (true)
          {
            case true when state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress: CreateAccounts(); break;
            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Stream:

              var adapter = View.Adapters["Prime"] as Schwab.Adapter;
              var account = adapter.Account;
              await adapter.SubscribeToDom(Instrument, DomEnum.Nyse);
              await OnData();
              break;
          }
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    protected virtual void CreateAccounts()
    {
      var account = new Account
      {
        Descriptor = Configuration["Schwab:Account"],
        State = new ConcurrentDictionary<string, StateModel>
        {
          [Instrument.Name] = new StateModel { Instrument = Instrument },
        },
      };

      var adapter = View.Adapters["Prime"] = new Adapter
      {
        Account = account,
        AccessToken = Configuration["Schwab:AccessToken"],
        RefreshToken = Configuration["Schwab:RefreshToken"],
        ClientId = Configuration["Schwab:ConsumerKey"],
        ClientSecret = Configuration["Schwab:ConsumerSecret"]
      };

      var interval = new Timer();

      interval.Elapsed += async (o, e) => await OnData();
      interval.Interval = 1000;
      interval.Enabled = true;
    }

    protected async Task OnData()
    {
      try
      {
        var adapter = View.Adapters["Prime"];
        var summary = adapter.Account.State[Instrument.Name];
        var optionArgs = new ConditionModel
        {
          Span = 50,
          MinDate = DateTime.Now,
          MaxDate = DateTime.Now.AddMonths(1),
          Instrument = Instrument
        };

        var domArgs = new ConditionModel
        {
          Instrument = Instrument
        };

        var options = await adapter.GetOptions(optionArgs);
        var storage = $"D:/Code/NET/Terminal/Data/{Instrument.Name}/{DateTime.Now:yyyy-MM-dd}";

        summary.Points.Clear();
        summary.PointGroups.Clear();
        summary.Options = options.Data;

        Directory.CreateDirectory(storage);

        var content = JsonSerializer.Serialize(summary, Srv.Options);
        var source = $"{storage}/{DateTime.UtcNow.Ticks}.zip";

        using var archive = ZipFile.Open(source, ZipArchiveMode.Create);
        using (var entry = archive.CreateEntry($"{DateTime.UtcNow.Ticks}").Open())
        {
          var bytes = Encoding.UTF8.GetBytes(content);
          entry.Write(bytes);
        }
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage(new MessageModel<string> { Error = e });
      }
    }
  }
}
