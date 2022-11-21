using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;

namespace Terminal.Client.Components
{
  public class BaseComponent : ComponentBase
  {
    /// <summary>
    /// Updater
    /// </summary>
    protected Task Updater { get; set; }

    /// <summary>
    /// Render
    /// </summary>
    protected Task Render(Action action)
    {
      if (Updater?.IsCompleted is false)
      {
        return Updater;
      }

      action();

      return Updater = InvokeAsync(StateHasChanged);
    }
  }
}
