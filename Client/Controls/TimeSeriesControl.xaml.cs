using Chart;
using Chart.ControlSpace;
using Chart.ModelSpace;
using Chart.SeriesSpace;
using Core.EnumSpace;
using Core.ModelSpace;
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Client.ControlSpace
{
  /// <summary>
  /// Control class
  /// </summary>
  public partial class TimeSeriesControl : UserControl
  {
    private IList<IChartModel> _groups = new List<IChartModel>();
    private IList<IDisposable> _disposables = new List<IDisposable>();
    private IList<ComponentComposer> _composers = new List<ComponentComposer>();
    private IList<Chart.ModelSpace.IPointModel> _points = new List<Chart.ModelSpace.IPointModel>();
    private IDictionary<long, Chart.ModelSpace.IPointModel> _cache = new Dictionary<long, Chart.ModelSpace.IPointModel>();

    /// <summary>
    /// Constructor
    /// </summary>
    public TimeSeriesControl()
    {
      InitializeComponent();

      var processors = InstanceManager<ResponseModel<IProcessorModel>>
        .Instance
        .Items;

      processors
        .Select(o => o.StateStream)
        .Merge()
        .Subscribe(message =>
        {
          if (Equals(message, StatusEnum.Connection))
          {
            CreateCharts();
            CreateSubscriptions();
          }
        });

      CreateCharts();
      CreateSubscriptions();
    }

    /// <summary>
    /// Create synced group of charts
    /// </summary>
    private void CreateCharts()
    {
      _disposables.ForEach(o => o.Dispose());

      _cache.Clear();
      _points.Clear();
      _composers.Clear();
      _disposables.Clear();

      ChartAreas.Children.Clear();
      ChartAreas.RowDefinitions.Clear();

      var index = 0;

      var processors = InstanceManager<ResponseModel<IProcessorModel>>
        .Instance
        .Items;

      // Create chart areas

      foreach (var areaModel in processors.SelectMany(processor => processor.Charts))
      {
        var seriesModels = areaModel.ChartData.ToDictionary(o => o.Key, o =>
        {
          var shape = new LineSeries() as ISeries;

          switch (o.Value.Shape)
          {
            case nameof(ShapeEnum.Bar): shape = new BarSeries(); break;
            case nameof(ShapeEnum.Line): shape = new LineSeries(); break;
            case nameof(ShapeEnum.Area): shape = new AreaSeries(); break;
            case nameof(ShapeEnum.Arrow): shape = new ArrowSeries(); break;
            case nameof(ShapeEnum.Candle): shape = new CandleSeries(); break;
          }

          return new SeriesModel
          {
            Name = o.Value.Name,
            Shape = shape

          } as ISeriesModel;
        });

        var inputAreaModel = new AreaModel
        {
          Name = areaModel.Name,
          Series = seriesModels
        };

        var chartControl = new ChartControl
        {
          Composers = _composers,
          Background = Brushes.Transparent,
          VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
          HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
        };

        var composer = new ComponentComposer
        {
          Group = inputAreaModel,
          Name = areaModel.Name,
          Control = chartControl,
          ShowIndexAction = (i) =>
          {
            var date = _points.ElementAtOrDefault(0)?.Time;

            if (i > 0)
            {
              date =
                _points.ElementAtOrDefault((int)i)?.Time ??
                _points.ElementAtOrDefault(_points.Count - 1)?.Time ??
                DateTime.Now;
            }

            return $"{date:yyyy-MM-dd HH:mm}";
          },
          ShowValueAction = areaModel.ShowValue
        };

        _composers.Add(chartControl.Composer = composer);

        ChartAreas.RowDefinitions.Add(new RowDefinition());
        ChartAreas.Children.Add(chartControl);
        Grid.SetRow(chartControl, index++);
      }

      foreach (var composer in _composers)
      {
        Dispatcher.BeginInvoke(new Action(() =>
        {
          composer.Create();
          composer.Update();

        }), DispatcherPriority.ApplicationIdle);
      }
    }

    /// <summary>
    /// Update charts after data source change
    /// </summary>
    private void UpdateComposers()
    {
      foreach (var composer in _composers)
      {
        Dispatcher.BeginInvoke(new Action(() =>
        {
          composer.IndexDomain ??= new int[2];
          composer.IndexDomain[0] = Math.Max(_points.Count - composer.IndexCount.Value, 0);
          composer.IndexDomain[1] = Math.Max(_points.Count, composer.IndexCount.Value);
          composer.Items = _points;
          composer.Update();

        }), DispatcherPriority.ApplicationIdle);
      }
    }

    /// <summary>
    /// Initialize data and order subscriptions
    /// </summary>
    /// <returns></returns>
    private void CreateSubscriptions()
    {
      var processors = InstanceManager<ResponseModel<IProcessorModel>>.Instance.Items;
      var charts = _groups = processors.SelectMany(o => o.Charts).ToList();
      var areaGroups = charts.GroupBy(o => o.Name).ToDictionary(o => o.Key, o => o.FirstOrDefault());
      var gateways = processors.SelectMany(processor => processor.Gateways);

      _disposables
        .Add(gateways
        .Select(gateway => gateway.DataStream)
        .Merge()
        .Subscribe(message =>
        {
          if (Equals(message.Action, ActionEnum.Create) || Equals(message.Action, ActionEnum.Update))
          {
            message.Next.Series[message.Next.Name] = message.Next;

            foreach (var seriesItem in message.Next.Series.Values)
            {
              if (seriesItem?.ChartData?.Area == null)
              {
                continue;
              }

              if (areaGroups.ContainsKey(seriesItem.ChartData.Area))
              {
                UpdatePoint(seriesItem);
              }
            }

            UpdateComposers();
          }
        }));

      _disposables
        .Add(gateways
        .Select(gateway => gateway.Account.ActiveOrders.CollectionStream)
        .Merge()
        .Subscribe(message =>
        {
          foreach (var composer in _composers)
          {
            Dispatcher.BeginInvoke(new Action(() =>
            {
              composer.UpdateLevels(null, message
                .Next
                .Where(o => Equals(composer.Name, o.Instrument.ChartData.Area))
                .Select(o => o.Price.Value)
                .ToList());

            }), DispatcherPriority.ApplicationIdle);
          }
        }));

      _disposables
        .Add(gateways
        .Select(gateway => gateway.Account.ActivePositions.ItemStream)
        .Merge()
        .Subscribe(message =>
        {
          var order = message.Next ?? message.Previous;

          if (Equals(message.Action, ActionEnum.Create))
          {
            var instrument = message
              .Next
              .Instrument;

            if (areaGroups.TryGetValue(instrument.ChartData.Area ?? string.Empty, out IChartModel chartModels))
            {
              var direction = 0.0;
              var dataPoint = _points.LastOrDefault();
              var chartData = chartModels.ChartData.FirstOrDefault(o => Equals(o.Value.Shape, nameof(ShapeEnum.Arrow))).Value;

              if (chartData == null || dataPoint == null)
              {
                return;
              }

              switch (order.Side)
              {
                case OrderSideEnum.Buy: direction = 1.0; break;
                case OrderSideEnum.Sell: direction = -1.0; break;
              }

              var pointModel = new Core.ModelSpace.PointModel
              {
                Last = order.Price,
                ChartData = chartData,
                Time = dataPoint.Time,
                Bar = new PointBarModel
                {
                  Close = order.Price
                }
              };

              UpdatePoint(pointModel, direction);
              UpdateComposers();
            }
          }
        }));
    }

    /// <summary>
    /// Get existing or create new point in the chart series
    /// </summary>
    /// <param name="pointModel"></param>
    /// <param name="direction"></param>
    private void UpdatePoint(Core.ModelSpace.IPointModel pointModel, double direction = 0.0)
    {
      if (pointModel.ChartData.Area == null || pointModel.ChartData.Name == null)
      {
        return;
      }

      dynamic value = new ExpandoObject();

      value.Direction = direction;
      value.Point = pointModel?.Last;
      value.Low = pointModel?.Bar?.Low;
      value.High = pointModel?.Bar?.High;
      value.Open = pointModel?.Bar?.Open;
      value.Close = pointModel?.Bar?.Close;

      var color = pointModel.ChartData.Color;

      switch (pointModel.ChartData.Shape)
      {
        case nameof(ShapeEnum.Line): value.Color = Brushes.Black.Color; break;
        case nameof(ShapeEnum.Arrow): value.Color = Brushes.Black.Color; break;
        case nameof(ShapeEnum.Bar): value.Color = value.Point > 0 ? Brushes.LimeGreen.Color : Brushes.OrangeRed.Color; break;
        case nameof(ShapeEnum.Area): value.Color = value.Point > 0 ? Brushes.LimeGreen.Color : Brushes.OrangeRed.Color; break;
        case nameof(ShapeEnum.Candle): value.Color = value.Close > value.Open ? Brushes.LimeGreen.Color : Brushes.OrangeRed.Color; break;
      }

      if (color.HasValue)
      {
        value.Color = Color.FromArgb(
          color.Value.A,
          color.Value.R,
          color.Value.G,
          color.Value.B);
      }

      // Update

      if (_cache.TryGetValue(pointModel.Time.Value.Ticks, out Chart.ModelSpace.IPointModel updateModel))
      {
        updateModel
          .Areas[pointModel.ChartData.Area]
          .Series[pointModel.ChartData.Name]
          .Model = value;

        return;
      }

      // Create

      var createModel = new Chart.ModelSpace.PointModel
      {
        Time = pointModel.Time.Value,
        Areas = new Dictionary<string, IAreaModel>()
      };

      foreach (var area in _groups)
      {
        createModel.Areas[area.Name] =
          createModel.Areas.ContainsKey(area.Name) ?
          createModel.Areas[area.Name] :
          new AreaModel
          {
            Name = area.Name,
            Series = new Dictionary<string, ISeriesModel>()
          };

        foreach (var series in area.ChartData)
        {
          createModel.Areas[area.Name].Series[series.Key] =
            createModel.Areas[area.Name].Series.ContainsKey(series.Key) ?
            createModel.Areas[area.Name].Series[series.Key] :
            new SeriesModel
            {
              Name = series.Key,
              Model = null
            };
        }
      }

      createModel
        .Areas[pointModel.ChartData.Area]
        .Series[pointModel.ChartData.Name]
        .Model = value;

      _cache[pointModel.Time.Value.Ticks] = createModel;
      _points.Add(createModel);
    }
  }
}
