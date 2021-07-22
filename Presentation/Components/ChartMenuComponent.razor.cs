using Core.ModelSpace;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;

namespace Presentation.Components
{
  public partial class ChartMenuComponent : IDisposable
  {
    [Parameter]
    public bool MenuState { get; set; }

    protected List<IProcessorModel> _processors = InstanceManager<ResponseModel<IProcessorModel>>.Instance.Items;

    protected void ConnectProcessors() => _processors.ForEach(o => o.Connect());
    protected void DisconnectProcessors() => _processors.ForEach(o => o.Disconnect());
    protected void SubscribeProcessors() => _processors.ForEach(o => o.Subscribe());
    protected void UnsubscribeProcessors() => _processors.ForEach(o => o.Unsubscribe());

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
      DisconnectProcessors();
    }
  }
}
