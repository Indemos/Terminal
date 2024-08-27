using Distribution.Models;
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
    protected virtual Task Render(Action action = null, bool isRemovable = true)
    {
      var options = new OptionModel
      {
        IsRemovable = false
      };

      return Updater.Send(() =>
      {
        if (action is not null)
        {
          action();
        }

        InvokeAsync(StateHasChanged);

      }, options).Task;
    }
  }
}
