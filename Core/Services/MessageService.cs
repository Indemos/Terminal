using System;

namespace Terminal.Core.Services
{
  public class MessageService
  {
    public virtual Action<string> OnMessage { get; set; }
  }
}
