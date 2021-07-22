using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Presentation.Components
{
  public partial class TableComponent : IDisposable
  {
    protected IList<dynamic> _items = new List<dynamic>();
    protected IEnumerable<string> _columns = new List<string>();

    /// <summary>
    /// List of table columns
    /// </summary>
    [Parameter]
    public IEnumerable<string> Columns { get => _columns; set => _columns = value; }

    /// <summary>
    /// List of table records
    /// </summary>
    [Parameter]
    public IList<dynamic> Items { get => _items; set => _items = value; }

    /// <summary>
    /// Page load processor
    /// </summary>
    /// <param name="setup"></param>
    /// <returns></returns>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await Task.Run(() => { });
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
    }
  }
}
