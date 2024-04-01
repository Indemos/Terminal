using Distribution.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Components
{
  public class BaseComponent : ComponentBase
  {
    private static object protection = new();

    /// <summary>
    /// Updater
    /// </summary>
    protected virtual ScheduleService Updater { get; set; } = new ScheduleService();

    /// <summary>
    /// Render
    /// </summary>
    protected virtual Task Render(Action action) => Updater.Send(() =>
    {
      lock (protection)
      {
        action();
        InvokeAsync(StateHasChanged);
        Thread.Sleep(1);
      }
    }).Task;
  }
}
