using Core.Services;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.Serialization;
using Orleans.TestingHost;

namespace Core.Tests
{
  public class SiloConfigurator : ISiloConfigurator, IClientBuilderConfigurator
  {
    public void Configure(ISiloBuilder orleans)
    {
      orleans.AddMemoryGrainStorageAsDefault();
      orleans.AddMemoryGrainStorage("PubSubStore");
      orleans.Services.AddSingleton<MessageService>();
      orleans.Services.AddSerializer(serializers =>
      {
        var converter = new ConversionService();

        serializers.AddMessagePackSerializer(
          o => true,
          o => true,
          o => o.Configure(options => options.SerializerOptions = converter.MessageOptions));
      });
    }

    public void Configure(IConfiguration configuration, IClientBuilder orleans)
    {
      orleans.Services.AddSingleton<MessageService>();
      orleans.Services.AddSerializer(serializers =>
      {
        var converter = new ConversionService();

        serializers.AddMessagePackSerializer(
          o => true,
          o => true,
          o => o.Configure(options => options.SerializerOptions = converter.MessageOptions));
      });
    }
  }
}
