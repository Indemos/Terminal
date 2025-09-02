using Core.Client.Models;
using System;

namespace Core.Client.Services
{
  public class MessageService
  {
    public virtual Action<MessageModel<string>> Update { get; set; } = o => { };
  }
}
