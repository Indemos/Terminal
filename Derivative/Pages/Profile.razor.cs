using Canvas.Core.Composers;
using Canvas.Core.Engines;
using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Canvas.Views.Web.Views;
using Derivative.Models;
using Derivative.Pages.Popups;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Enums;
using Terminal.Core.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Derivative.Pages
{
  public partial class Profile
  {
    [Inject] IDialogService ModalService { get; set; }

    protected int Count { get; set; } = 1;
    protected bool IsLoading { get; set; }
    protected Dictionary<string, Dictionary<string, CanvasView>> Groups { get; set; } = [];

    /// <summary>
    /// Clear
    /// </summary>
    public void OnClear()
    {
      Count = 1;
      Groups.Clear();
    }

    /// <summary>
    /// Bar chart editor
    /// </summary>
    /// <returns></returns>
    public async Task OnCombine()
    {
      await OnChart<OptionEditor>(async (caption, response) =>
      {
        Groups[caption] = new Dictionary<string, CanvasView> { [caption] = null };
        await InvokeAsync(StateHasChanged);
        Groups[caption].ForEach(async o => await Show(o.Value, response));
      });
    }

    /// <summary>
    /// API query to get options by criteria
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="action"></param>
    /// <returns></returns>
    protected async Task OnChart<T>(Func<string, OptionInputModel, Task> action) where T : ComponentBase
    {
      var props = new DialogOptions
      {
        FullWidth = true,
        MaxWidth = MaxWidth.ExtraSmall,
        CloseOnEscapeKey = true
      };

      var response = await ModalService
        .ShowAsync<T>("Editor", props)
        .ContinueWith(async process =>
        {
          IsLoading = true;

          var popup = await process;
          var response = await popup.Result;

          if (response.Canceled is false)
          {
            var inputModel = response.Data as OptionInputModel;
            var caption = $"{Count++} : {inputModel.Name}";

            await action(caption, inputModel);
          }

          IsLoading = false;

          await InvokeAsync(StateHasChanged);
        });
    }

    /// <summary>
    /// Show bar charts
    /// </summary>
    /// <param name="view"></param>
    /// <param name="items"></param>
    /// <returns></returns>
    protected async Task Show(CanvasView view, OptionInputModel inputModel)
    {
      var min = Math.Min(inputModel.Price, inputModel.Strike);
      var max = Math.Max(inputModel.Price, inputModel.Strike);
      var prices = Enumerable.Range((int)(min - min / 2), (int)(max + max / 2));
      var chartPoints = prices.Select((o, i) =>
      {
        var direction = inputModel.Position is OrderSideEnum.Buy ? 1.0 : -1.0;

        return new LineShape
        {
          X = i,
          Y = (OptionService.Premium(inputModel.Side, i, inputModel.Strike, 0.000001, 0.25, 0.05, 0) - inputModel.Premium) * direction,
          Component = new ComponentModel { Size = 2, Color = SKColors.DeepSkyBlue }

        } as IShape;

      }).ToList();

      var composer = new Composer
      {
        Space = 0.05,
        Name = inputModel.Name,
        Items = chartPoints,
        View = view
      };

      await composer.Create<CanvasEngine>();
      await composer.Update();
    }
  }
}
