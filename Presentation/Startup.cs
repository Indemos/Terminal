using Core.ModelSpace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Presentation.Services;
using Presentation.StrategySpace;
using System.Collections.Generic;

namespace Presentation
{
  public class Startup
  {
    public static IConfiguration Configuration { get; private set; }

    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
      InstanceManager<ResponseModel<IProcessorModel>>.Instance.Items = new List<IProcessorModel>
      {
        new ReverseStrategy()
      };

      services.AddRazorPages();
      services.AddServerSideBlazor().AddHubOptions(o => o.MaximumReceiveMessageSize = 100 * 1024 * 1024);
      services.AddTransient<CommandService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      app.UseDeveloperExceptionPage();
      app.UseHttpsRedirection();
      app.UseStaticFiles();
      app.UseRouting();
      app.UseEndpoints(endpoints =>
      {
        endpoints.MapBlazorHub();
        endpoints.MapFallbackToPage("/Host");
      });
    }
  }
}
