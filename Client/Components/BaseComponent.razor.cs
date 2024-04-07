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
    protected virtual ScheduleService Updater { get; set; } = InstanceService<ScheduleService>.Instance;

    /// <summary>
    /// Render
    /// </summary>
    protected virtual Task Render(Action action) => Updater.Send(() =>
    {
      action();
      InvokeAsync(StateHasChanged);
    }).Task;
  }
}
