using Canvas.Core.Shapes;
using Core.Conventions;
using Core.Enums;
using Core.Models;
using Core.Services;
using Estimator.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Board.Components
{
  public partial class EstimatesComponent
  {
    [Inject] public virtual SubscriptionService Observer { get; set; }

    [Parameter] public virtual string Name { get; set; }

    public class OptionInputModel
    {
      public double Price { get; set; }
      public double Strike { get; set; }
      public double Premium { get; set; }
      public double Amount { get; set; }
      public DateTime? Date { get; set; }
      public OptionSideEnum Side { get; set; }
      public OrderSideEnum Position { get; set; }
    }

    /// <summary>
    /// Chart control
    /// </summary>
    protected virtual ChartsComponent ChartsView { get; set; }

    /// <summary>
    /// Views
    /// </summary>
    public virtual void Clear() => ChartsView.Clear();

    /// <summary>
    /// Views
    /// </summary>
    public virtual async Task Create(string name) => await ChartsView.Create(name);

    /// <summary>
    /// Setup views
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await base.OnAfterRenderAsync(setup);

      if (setup)
      {
        Observer.OnState += state =>
        {
          if (state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.None)
          {
            Clear();
          }
        };
      }
    }

    /// <summary>
    /// Render estimated position gain
    /// </summary>
    /// <param name="adapter"></param>
    /// <param name="point"></param>
    /// <param name="positions"></param>
    public virtual void UpdateItems(IGateway adapter, PriceModel point, IEnumerable<OrderModel> positions)
    {
      if (Observer.State.Next is SubscriptionEnum.None)
      {
        return;
      }

      var sums = new Dictionary<double, double>();

      foreach (var pos in positions)
      {
        var plusPercents = Enumerable.Range(0, 20).Select((o, i) => o / 2.0 / 100.0);
        var minusPercents = Enumerable.Range(1, 20).Select((o, i) => -o / 2.0 / 100.0).Reverse();
        var inputModel = new OptionInputModel
        {
          Price = point.Last.Value,
          Amount = pos.Amount ?? 0,
          Strike = pos.Operation.Instrument?.Derivative?.Strike ?? 0,
          Premium = pos.Operation.Instrument?.Price?.Last ?? 0,
          Date = pos.Operation.Instrument?.Derivative?.ExpirationDate,
          Side = pos.Operation.Instrument?.Derivative?.Side ?? 0,
          Position = pos.Side.Value
        };

        var chartPoints = minusPercents.Concat(plusPercents).Select((o, i) =>
        {
          var step = inputModel.Price + inputModel.Price * o;
          var sum = GetEstimate(step, new DateTime(point.Time.Value), inputModel);
          var shape = new Shape();

          sums[o] = sums.TryGetValue(o, out var s) ? s + sum : sum;

          shape.X = step;
          shape.Groups = new ConcurrentDictionary<string, IShape>();
          shape.Groups["Estimates"] = new Shape();
          shape.Groups["Estimates"].Groups = new ConcurrentDictionary<string, IShape>();
          shape.Groups["Estimates"].Groups["Value"] = new LineShape { Name = "Value", Y = sums[o] };

          return shape as IShape;

        }).ToList();

        ChartsView.Composers.ForEach(composer => composer.ShowIndex = o => $"{chartPoints.ElementAtOrDefault((int)o)?.X:0.00}");
        ChartsView.UpdateOrdinals(chartPoints);
      }
    }

    /// <summary>
    /// Estimated PnL for shares or options
    /// </summary>
    /// <param name="price"></param>
    /// <param name="inputModel"></param>
    protected virtual double GetEstimate(double price, DateTime date, OptionInputModel inputModel)
    {
      var direction = inputModel.Position is OrderSideEnum.Long ? 1.0 : -1.0;

      if (inputModel.Side is not (OptionSideEnum.Put or OptionSideEnum.Call))
      {
        return (price - inputModel.Price) * inputModel.Amount * direction;
      }

      var optionSide = Enum.GetName(inputModel.Side.GetType(), inputModel.Side);
      var days = Math.Max((inputModel.Date - date).Value.TotalDays / 250.0, double.Epsilon);
      var estimate = OptionService.Price(optionSide, price, inputModel.Strike, days, 0.25, 0.05, 0);

      return (estimate - inputModel.Premium) * inputModel.Amount * direction * 100.0;
    }
  }
}
