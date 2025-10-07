using Core.Conventions;
using Core.Enums;
using Core.Models;

namespace Core.Messengers.Streams
{
  public class MessageStream(string source) : StreamMessenger<MessageModel>(source, nameof(StreamEnum.Message))
  {
  }
}
