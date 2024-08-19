using Distribution.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;

namespace Terminal.Components
{
  public class BaseComponent : ComponentBase
  {
    /// <summary>
    /// Updater
    /// </summary>
    protected virtual ScheduleService Updater { get; set; } = InstanceService<ScheduleService>.Instance;

    /// <summary>
    /// Render
    /// </summary>
    protected virtual Task Render(Action action, bool isRemovable = true) => Updater.Send(() =>
    {
      action();
      InvokeAsync(StateHasChanged);
    }, isRemovable).Task;
  }
}
