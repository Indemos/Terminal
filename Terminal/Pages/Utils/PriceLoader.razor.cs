using Distribution.Services;
using Distribution.Stream;
using InteractiveBrokers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Terminal.Components;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;
using Terminal.Core.Services;
using Terminal.Services;

namespace Terminal.Pages.Utils
{
  public partial class PriceLoader
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual ControlsComponent View { get; set; }
    protected virtual Service Srv { get; set; } = new Service();
    protected virtual InstrumentModel Instrument { get; set; } = new InstrumentModel { Name = "ESH5", Exchange = "CME", Type = InstrumentEnum.Futures, TimeFrame = TimeSpan.FromMinutes(1) };

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        InstanceService<SubscriptionService>.Instance.Update += async state =>
        {
          switch (true)
          {
            case true when state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress:

              CreateAccounts();
              break;

            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Stream:

              var stopDate = DateTime.UtcNow.AddDays(-3);
              var args = new ConditionModel
              {
                Span = 1000,
                MaxDate = DateTime.UtcNow,
                Instrument = Instrument
              };

              while (args.MaxDate is not null && args.MaxDate > stopDate)
              {
                args.MaxDate = await OnData(args);
              }

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
        Descriptor = Configuration["InteractiveBrokers:Account"],
        State = new Map<string, SummaryModel>
        {
          [Instrument.Name] = new SummaryModel { Instrument = Instrument }
        }        
      };

      View.Adapters["Prime"] = new Adapter
      {
        Account = account,
        Port = int.Parse(Configuration["InteractiveBrokers:Port"])
      };
    }

    protected async Task<DateTime?> OnData(ConditionModel criteria)
    {
      try
      {
        var counter = 0;
        var adapter = View.Adapters["Prime"];
        var points = await adapter.GetPoints(criteria);
        var storage = $"D:/Code/NET/Terminal/Data/{Instrument.Name}";

        Directory.CreateDirectory(storage);

        foreach (var point in points.Data)
        {
          var content = JsonSerializer.Serialize(point);
          var source = $"{storage}/{point?.Time?.Ticks}-{++counter}";

          await File.WriteAllTextAsync(source, content);
        }

        return points?.Data?.FirstOrDefault()?.Time;
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.Update(new MessageModel<string> { Error = e });
      }

      return null;
    }
  }
}
