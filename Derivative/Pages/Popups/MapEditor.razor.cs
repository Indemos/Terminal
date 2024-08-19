using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using Derivative.Models;
using System;

namespace Derivative.Pages.Popups
{
  public partial class MapEditor : ComponentBase
  {
    [Inject] ISnackbar Snackbar { get; set; }
    [Inject] IDialogService ModalService { get; set; }
    [Inject] IConfiguration Configuration { get; set; }

    [CascadingParameter] MudDialogInstance Popup { get; set; }

    protected MapInputModel InputModel { get; set; } = new MapInputModel
    {
      Name = "SPY",
      Group = "Yes",
      Range = new(DateTime.Now.Date, DateTime.Now.Date),
      Expression = "(CVolume - COpenInterest) * CGamma - (PVolume - POpenInterest) * PGamma"
    };

    protected EditContext InputContext { get; set; }
    protected override void OnInitialized() => InputContext = new(InputModel);
    protected void OnSuccess() => Popup.Close(DialogResult.Ok(InputModel));
    protected void OnClose() => Popup.Cancel();
  }
}
