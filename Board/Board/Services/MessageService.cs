using Core.Enums;
using Core.Models;
using Orleans;
using Orleans.Streams;
using System;

namespace Board.Services
{
  public class MessageService
  {
    protected SubscriptionModel state = new() { Next = SubscriptionEnum.None };

    /// <summary>
    /// Cluster client
    /// </summary>
    public virtual IClusterClient Connector { get; protected set; }

    /// <summary>
    /// Connector
    /// </summary>
    /// <param name="connector"></param>
    public MessageService(IClusterClient connector) => Connector = connector;

    /// <summary>
    /// Stream
    /// </summary>
    public virtual IAsyncStream<MessageModel> Stream => Connector
      .GetStreamProvider(nameof(StreamEnum.Message))
      .GetStream<MessageModel>(string.Empty, Guid.Empty);

    /// <summary>
    /// Push notification
    /// </summary>
    public virtual Action<SubscriptionModel> OnMessage { get; set; } = delegate { };

    /// <summary>
    /// Subscription instance
    /// </summary>
    public virtual SubscriptionModel State
    {
      get => state;
      set => OnMessage(state = value);
    }
  }
}
