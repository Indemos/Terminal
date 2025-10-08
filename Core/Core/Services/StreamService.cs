using Core.Messengers.Streams;

namespace Core.Services
{
  public class StreamService(PriceStream priceStream, OrderStream orderStream, MessageStream messageStream)
  {
    /// <summary>
    /// Price stream
    /// </summary>
    public virtual PriceStream Prices { get; } = priceStream;

    /// <summary>
    /// Order stream
    /// </summary>
    public virtual OrderStream Orders { get; } = orderStream;

    /// <summary>
    /// Message stream
    /// </summary>
    public virtual MessageStream Messages { get; } = messageStream;
  }
}
