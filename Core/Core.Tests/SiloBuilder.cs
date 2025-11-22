using Core.Models;
using Core.Services;
using Microsoft.Extensions.Configuration;
using Orleans.Hosting;
using Orleans.Providers;
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
      orleans.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(Message));
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
      orleans.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(Message));
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
