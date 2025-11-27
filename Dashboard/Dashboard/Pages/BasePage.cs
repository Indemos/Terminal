using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Core.Conventions;
using Core.Enums;
using Core.Models;
using Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using Orleans;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages
{
  public class BasePage : ComponentBase
  {
    [Inject] protected virtual IJSRuntime RuntimeService { get; set; }
    [Inject] protected virtual IClusterClient Connector { get; set; }
    [Inject] protected virtual IConfiguration Configuration { get; set; }
    [Inject] protected virtual StateService State { get; set; }

    /// <summary>
    /// Chart settings
    /// </summary>
    protected virtual ComponentModel Com { get; set; } = new() { Color = SKColors.LimeGreen };
    protected virtual ComponentModel ComUp { get; set; } = new() { Color = SKColors.DeepSkyBlue };
    protected virtual ComponentModel ComDown { get; set; } = new() { Color = SKColors.OrangeRed };

    /// <summary>
    /// Gateways
    /// </summary>
    public virtual IDictionary<string, IGateway> Adapters { get; set; } = new Dictionary<string, IGateway>();

    /// <summary>
    /// Primary gateway
    /// </summary>
    public virtual IGateway Adapter
    {
      get => Adapters[string.Empty];
      set => Adapters[string.Empty] = value;
    }

    /// <summary>
    /// View and subscription setup
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await OnView();

        State.Subscribe(async state =>
        {
          if (state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress)
          {
            await OnTrade();

            foreach (var adapter in Adapters.Values)
            {
              adapter.OnPrice = OnViewUpdate;
              adapter.OnInstrument = OnTradeUpdate;
            }
          }
        });
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// Processors
    /// </summary>
    protected virtual Task OnView() => Task.CompletedTask;
    protected virtual Task OnTrade() => Task.CompletedTask;
    protected virtual void OnViewUpdate(Instrument instrument) { }
    protected virtual Task OnTradeUpdate(Instrument instrument) => Task.CompletedTask;

    /// <summary>
    /// Time axis renderer
    /// </summary>
    /// <param name="items"></param>
    /// <param name="index"></param>
    protected virtual string GetDate(IList<IShape> items, int index)
    {
      var empty = index <= 0 ? items.FirstOrDefault()?.X : items.LastOrDefault()?.X;
      var stamp = (long)(items.ElementAtOrDefault(index)?.X ?? empty ?? DateTime.Now.Ticks);

      return $"{new DateTime(stamp):HH:mm}";
    }

    /// <summary>
    /// Open position
    /// </summary>
    /// <param name="asset"></param>
    /// <param name="side"></param>
    protected virtual async Task OpenPosition(IGateway adapter, Instrument asset, OrderSideEnum side)
    {
      var order = new Order
      {
        Amount = 1,
        Side = side,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = asset }
      };

      await adapter.SendOrder(order);
    }

    /// <summary>
    /// Close positions
    /// </summary>
    /// <param name="condition"></param>
    protected virtual async Task ClosePosition(IGateway adapter, Func<Order, bool> condition = null)
    {
      var positions = await adapter.GetPositions(default);

      foreach (var position in positions.Data)
      {
        if (condition is null || condition(position))
        {
          var order = new Order
          {
            Amount = position.Operation.Amount,
            Side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long,
            Type = OrderTypeEnum.Market,
            Operation = new()
            {
              Instrument = position.Operation.Instrument
            }
          };

          await adapter.SendOrder(order);
        }
      }
    }
  }
}
