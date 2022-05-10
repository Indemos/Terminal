using System;
using System.Threading.Tasks;
using Terminal.Core.ModelSpace;

namespace Terminal.Connector.Simulation
{
  /// <summary>
  /// Implementation
  /// </summary>
  public class Generator : Adapter
  {
    /// <summary>
    /// Connect to the server
    /// </summary>
    /// <param name="docHeader"></param>
    public override Task Connect()
    {
      var generator = new Random();
      var price = generator.NextDouble();
      var days = generator.NextDouble() * 10;

      _points.Clear();

      foreach (var instrument in Account.Instruments)
      {
        _points[instrument.Key] = new PointModel
        {
          Ask = price,
          Bid = price + generator.NextDouble(),
          Last = price,
          Account = Account,
          Instrument = instrument.Value,
          Time = DateTime.Now.AddDays(-days),
          TimeFrame = instrument.Value.TimeFrame,
          //ChartData = instrument.Value.ChartData,
          Group = new PointGroupModel
          {
            Low = price,
            High = price,
            Open = price,
            Close = price
          }
        };
      }

      Subscribe();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Add data point to the collection
    /// </summary>
    /// <returns></returns>
    protected override void GeneratePoints()
    {
      var generator = new Random();
      var span = TimeSpan.FromSeconds(10);

      foreach (var instrument in Account.Instruments)
      {
        var model = new PointModel();
        var point = _points[instrument.Key];

        point.Group ??= new PointGroupModel();

        model.Instrument = instrument.Value;
        model.Ask = point.Group.Close + generator.NextDouble() * (10.0 - 1.0) + 1.0 - 5.0;
        model.Bid = model.Ask - generator.NextDouble() * 5.0;
        model.Time = point.Time;

        // Next values

        point.Time = point.Time.Value.AddTicks(span.Ticks);
        point.Group.Close = model.Ask;

        UpdatePoints(model);
      }
    }
  }
}
