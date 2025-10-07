using Core.Conventions;
using Core.Enums;
using Core.Models;

namespace Core.Messengers.Streams
{
  public class PriceStream(string source) : StreamMessenger<PriceModel>(source, nameof(StreamEnum.Price))
  {
  }
}
