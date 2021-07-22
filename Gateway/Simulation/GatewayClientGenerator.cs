using Core.ModelSpace;
using System;
using System.Threading.Tasks;

namespace Gateway.Simulation
{
  /// <summary>
  /// Implementation
  /// </summary>
  public class GatewayClientGenerator : GatewayClient
  {
    /// <summary>
    /// Establish connection with a server
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
          Chart = instrument.Value.Chart,
          Bar = new PointBarModel
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
    protected override Task GeneratePoints()
    {
      var generator = new Random();
      var span = TimeSpan.FromSeconds(10);

      foreach (var instrument in Account.Instruments)
      {
        var model = new PointModel();
        var point = _points[instrument.Key];

        point.Bar ??= new PointBarModel();

        model.Ask = point.Bar.Close + generator.NextDouble() * (10.0 - 1.0) + 1.0 - 5.0;
        model.Bid = model.Ask - generator.NextDouble() * 5.0;
        model.Time = point.Time;

        // Next values

        point.Time = point.Time.Value.AddTicks(span.Ticks);
        point.Bar.Close = model.Ask;

        UpdatePointParams(model, instrument.Value);
      }

      return Task.FromResult(0);
    }
  }
}
