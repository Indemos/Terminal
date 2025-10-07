using Core.Conventions;
using Core.Enums;
using Core.Models;

namespace Core.Messengers.Streams
{
  public class OrderStream(string source) : StreamMessenger<OrderModel>(source, nameof(StreamEnum.Order))
  {
  }
}
