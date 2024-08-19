using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using Derivative.Models;
using System;

namespace Derivative.Pages.Popups
{
  public partial class PortfolioEditor : ComponentBase
  {
    [Inject] ISnackbar Snackbar { get; set; }
    [Inject] IDialogService ModalService { get; set; }
    [Inject] IConfiguration Configuration { get; set; }

    [CascadingParameter] MudDialogInstance Popup { get; set; }

    protected PortfolioInputModel InputModel { get; set; } = new PortfolioInputModel
    {
      Slope = 45,
      Count = 250,
      Duration = "Year",
      Resolution = "Daily",
      Names = "SPY,IVV,VOO",
    };

    protected EditContext InputContext { get; set; }
    protected override void OnInitialized() => InputContext = new(InputModel);
    protected void OnSuccess() => Popup.Close(DialogResult.Ok(InputModel));
    protected void OnClose() => Popup.Cancel();
  }
}
