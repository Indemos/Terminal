using System;

namespace Terminal.Core.Services
{
  public class NotificationService
  {
    public virtual Action<string> OnMessage { get; set; }
  }
}
