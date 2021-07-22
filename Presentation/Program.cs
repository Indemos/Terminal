using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Threading.Tasks;

namespace Presentation
{
  public class Program
  {
    public static async Task Main(string[] args)
    {
      await CreateWebHostBuilder(args).Build().RunAsync();
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args)
    {
      var configuration = new ConfigurationBuilder()
        .AddCommandLine(args)
        .Build();

      var environment = WebHost
        .CreateDefaultBuilder(args)
        .UseContentRoot(Directory.GetCurrentDirectory())
        .UseIISIntegration()
        .UseStartup<Startup>()
        .UseWebRoot("Assets")
        .UseStaticWebAssets()
        .UseConfiguration(configuration);

      return environment;
    }
  }
}
