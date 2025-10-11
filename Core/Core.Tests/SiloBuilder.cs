using Core.Extensions;
using Core.Services;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.Serialization;
using Orleans.TestingHost;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Core.Tests
{
  public class StreamServiceStub(string source) : StreamService(source)
  {
    Dictionary<string, List<Delegate>> _subscribers = new();

    public override Task Send<T>(T message)
    {
      _subscribers.Get(typeof(T).Name)?.ForEach(o => o.DynamicInvoke(message));
      return Task.CompletedTask;
    }

    public override Task Subscribe<T>(Action<T> action)
    {
      _subscribers[typeof(T).Name] = [.. _subscribers.Get(typeof(T).Name) ?? [], action];
      return Task.CompletedTask;
    }
  }

  public class SiloConfigurator : ISiloConfigurator, IClientBuilderConfigurator
  {
    public void Configure(ISiloBuilder orleans)
    {
      orleans.AddMemoryGrainStorageAsDefault();
      orleans.AddMemoryGrainStorage("PubSubStore");
      orleans.Services.AddSingleton<StreamService>(o => new StreamServiceStub($"http://{IPAddress.Loopback}:1000"));
      orleans.Services.AddSerializer(serializers =>
      {
        var messageOptions = MessagePackSerializerOptions
          .Standard
          .WithResolver(ContractlessStandardResolver.Instance);

        serializers.AddMessagePackSerializer(
          o => true,
          o => true,
          o => o.Configure(options => options.SerializerOptions = messageOptions));
      });
    }

    public void Configure(IConfiguration configuration, IClientBuilder orleans)
    {
      orleans.Services.AddSerializer(serializers =>
      {
        var messageOptions = MessagePackSerializerOptions
          .Standard
          .WithResolver(ContractlessStandardResolver.Instance);

        serializers.AddMessagePackSerializer(
          o => true,
          o => true,
          o => o.Configure(options => options.SerializerOptions = messageOptions));
      });
    }
  }
}
