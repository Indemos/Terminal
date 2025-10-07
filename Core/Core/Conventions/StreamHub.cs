using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Core.Conventions
{
  public class StreamHub : Hub
  {
    public Task Send(string descriptor, object message) => Clients.All.SendAsync(descriptor, message);
  }
}
