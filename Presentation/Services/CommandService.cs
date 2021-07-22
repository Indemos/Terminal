using Core.EnumSpace;
using Core.ModelSpace;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Presentation.Services
{
  // TODO: Improve serialization when NET Core exposes serializer options

  /// <summary>
  /// Interoperability service to exchange data with UI
  /// </summary>
  public class CommandService
  {
    /// <summary>
    /// Unix time stamp
    /// </summary>
    public static DateTime UnixTime { get; set; } = new DateTime(1970, 1, 1);

    /// <summary>
    /// Initialize charts
    /// </summary>
    /// <param name="scriptRuntime"></param>
    /// <param name="id"></param>
    /// <param name="areas"></param>
    /// <returns></returns>
    public async Task CreateCharts(IJSRuntime scriptRuntime, string id, IEnumerable<IChartModel> areas)
    {
      var message = JsonConvert.SerializeObject(areas.Select(o => new
      {
        o.ChartArea,
        o.ChartType,
        o.Name
      }));

      await Execute(scriptRuntime, "ChartFunctions.CreateCharts", id, message);
    }

    /// <summary>
    /// Clear charts
    /// </summary>
    /// <param name="scriptRuntime"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task DeleteCharts(IJSRuntime scriptRuntime, string id)
    {
      await Execute(scriptRuntime, "ChartFunctions.DeleteCharts", id);
    }

    /// <summary>
    /// Update series on specified charts
    /// </summary>
    /// <param name="scriptRuntime"></param>
    /// <param name="points"></param>
    /// <returns></returns>
    public async Task UpdatePoints(IJSRuntime scriptRuntime, IEnumerable<IPointModel> points)
    {
      var message = JsonConvert.SerializeObject(points.Select(model =>
      {
        dynamic chartParams = model.Chart;

        return new
        {
          model.Chart,
          model.Bar.Low,
          model.Bar.High,
          model.Bar.Open,
          model.Bar.Close,
          Time = model.Time.Value.Subtract(UnixTime).TotalMilliseconds
        };
      }));

      await Execute(scriptRuntime, "ChartFunctions.UpdatePoints", message);
    }

    /// <summary>
    /// Update series on specified charts
    /// </summary>
    /// <param name="scriptRuntime"></param>
    /// <param name="chartModel"></param>
    /// <param name="orders"></param>
    /// <returns></returns>
    public async Task UpdateTransactions(IJSRuntime scriptRuntime, IChartModel chartModel, IEnumerable<ITransactionOrderModel> orders)
    {
      var message = JsonConvert.SerializeObject(orders.Select(model =>
      {
        return new
        {
          Chart = chartModel,
          Last = model.Price,
          Close = model.Price,
          Time = model.Time.Value.Subtract(UnixTime).TotalMilliseconds,
          TransactionType = Enum.GetName(typeof(TransactionTypeEnum), model.Type)
        };
      }));

      await Execute(scriptRuntime, "ChartFunctions.UpdateTransactions", message);
    }

    /// <summary>
    /// Update orders on specified charts
    /// </summary>
    /// <param name="scriptRuntime"></param>
    /// <param name="orders"></param>
    /// <returns></returns>
    public async Task UpdateLevels(IJSRuntime scriptRuntime, IEnumerable<ITransactionOrderModel> orders)
    {
      var message = JsonConvert.SerializeObject(orders.Select(model => new
      {
        Last = model.Price,
        Close = model.Price,
        Chart = model.Instrument.Chart
      }));

      await Execute(scriptRuntime, "ChartFunctions.UpdateLevels", message);
    }

    /// <summary>
    /// Intercept potential cancellation of resource
    /// </summary>
    /// <param name="action"></param>
    protected async Task Execute(IJSRuntime scriptRuntime, string action, params object[] inputs)
    {
      try
      {
        await scriptRuntime.InvokeVoidAsync(action, inputs);
      }
      catch (Exception)
      {
      }

      await Task.FromResult(0);
    }
  }
}
