using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Core.Services
{
  public class StreamHubService : Hub
  {
    public Task Send(string descriptor, object message) => Clients.All.SendAsync(descriptor, message);
  }
}
