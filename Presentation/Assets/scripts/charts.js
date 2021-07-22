/**
 * Synced chart areas
 */
class ChartObserver {

  /**
   * Constructor
   */
  constructor() {
    this.observers = {}
  }

  /**
   * Get reference to subscriptions for specified event
   * @param {any} eventName
   */
  events(eventName) {
    return this.observers[eventName] = this.observers[eventName] || {};
  }

  /**
   * Send notification to subscribers
   * @param {any} eventName
   * @param {any} sourceName
   * @param {any} args
   */
  send(eventName, sourceName, ...args) {
    for (let name in (this.observers[eventName] || {})) {
      if (name !== sourceName && this.observers[eventName][name]) {
        this.observers[eventName][name](...args);
      }
    }
  }
}

/**
 * Synced data points
 */
class ChartData {

  /**
   * Constructor
   * @param {any} descriptor
   * @param {any} areas
   */
  constructor(descriptor, areas) {

    this.Count = 50;
    this.Areas = {};
    this.DataPoints = [];
    this.DataIndexes = {};

    const chartObserver = new ChartObserver();

    for (let i in areas) {
      this.Areas[i] = {
        Series: areas[i],
        Instance: new ChartControl(chartObserver, i, areas[i], descriptor)
      }
    }
  }

  /**
   * Get data point by date
   * @param {any} x
   */
  GetPoint(x) {
    return this.DataPoints[this.GetPointIndex(x)] || null;
  }

  /**
   * Get index of the data point by date
   * @param {any} x
   */
  GetPointIndex(x) {
    return this.DataIndexes[x] >= 0 ? this.DataIndexes[x] : null;
  }

  /**
   * Update data point on the chart 
   * @param {any} nextPoint
   */
  UpdatePoint(nextPoint = null) {

    const areaName = nextPoint.Chart.ChartArea;
    const seriesName = nextPoint.Chart.Name;
    const previousPoint = this.GetPoint(nextPoint.Time);

    previousPoint.Areas = previousPoint.Areas || {};
    previousPoint.Areas[areaName] = previousPoint.Areas[areaName] || {};
    previousPoint.Areas[areaName].Series = previousPoint.Areas[areaName].Series || {};
    previousPoint.Areas[areaName].Series[seriesName] = previousPoint.Areas[areaName][seriesName] || {};
    previousPoint.Areas[areaName].Series[seriesName].Low = nextPoint.Low;
    previousPoint.Areas[areaName].Series[seriesName].High = nextPoint.High;
    previousPoint.Areas[areaName].Series[seriesName].Open = nextPoint.Open;
    previousPoint.Areas[areaName].Series[seriesName].Close = nextPoint.Close;
    previousPoint.Areas[areaName].Series[seriesName].Chart = nextPoint.Chart;
    previousPoint.Areas[areaName].Transactions = [
      ...(previousPoint.Areas[areaName].Transactions || []),
      ...(nextPoint.Transactions || [])];
  }

  /**
   * Add new data point to the chart
   * @param {any} points
   */
  UpdatePoints(points = []) {

    for (let i = 0; i < points.length; i++) {

      const nextPoint = { ...{}, ...points[i] };
      const nextChart = nextPoint.Chart || {};
      const nextDate = nextPoint.Time;

      if (this.GetPoint(nextDate)) {

        this.UpdatePoint(nextPoint);

      } else {

        const nextIndex = this.DataPoints.length;
        const previousPoint = this.DataPoints[this.DataPoints.length - 1] || {};
        const point = { Time: nextDate, Index: nextIndex, Areas: {} };

        // Populate next point series with previous values by default

        for (let areaName in this.Areas) {

          previousPoint.Areas = previousPoint.Areas || {};
          previousPoint.Areas[areaName] = previousPoint.Areas[areaName] || {};
          previousPoint.Areas[areaName].Series = previousPoint.Areas[areaName].Series || {};

          for (let seriesName in this.Areas[areaName].Series) {

            const series =
              previousPoint.Areas[areaName].Series[seriesName] =
              previousPoint.Areas[areaName].Series[seriesName] || {};

            const previousType = ((series || {}).Chart || {}).ChartType;

            point.Areas[areaName] = point.Areas[areaName] || {};
            point.Areas[areaName].Series = point.Areas[areaName].Series || {};
            point.Areas[areaName].Series[seriesName] = ChartControl.ChartType[previousType] ? { ...series } : {};
          }
        }

        // Set actual value for the next point

        point.Areas[nextChart.ChartArea].Series[nextChart.Name] = { ...nextPoint };
        point.Areas[nextChart.ChartArea].Transactions = nextPoint.Transactions || [];

        this.DataPoints.push(point);
        this.DataIndexes[nextDate] = Math.max(this.DataPoints.length - 1, 0);
      }

      const maxX = this.DataPoints.length - 1;
      const minX = Math.max(this.DataPoints.length - this.Count, 0);

      for (let i in this.Areas) {
        this.Areas[i].Instance.DataPoints = this.DataPoints;
        this.Areas[i].Instance.Scale([minX, maxX], null, this.Count);
      }
    }
  }
}

/**
 * Chart UI control
 */
class ChartControl {

  /**
   * Deal types
   */
  static TransactionType = {
    Buy: 'Buy',
    Sell: 'Sell'
  };

  /**
   * Chart types
   */
  static ChartType = {
    Bar: 'Bar',
    Line: 'Line',
    Area: 'Area',
    Candle: 'Candle'
  };

  /**
   * Types that can interact with each other
   */
  static ChartTransactionType = {
    Deal: 'Deal'
  };

  /**
   * Constructor
   * @param {any} chartObserver
   * @param {any} name
   * @param {any} series
   * @param {any} descriptor
   */
  constructor(chartObserver, name, series, descriptor) {

    const seriesItems = [];

    this.MoveX = 0;
    this.MoveY = 0;
    this.ChartArea = name;
    this.ChartSeries = series;
    this.LevelsX = [];
    this.LevelsY = [];
    this.CrossLines = [];
    this.DataPoints = [];
    this.DataBounds = [];
    this.DomainX = [0, 0];
    this.DomainY = [0, 0];
    this.ScaleX = d3.scaleLinear();
    this.ScaleY = d3.scaleLinear();
    this.ChartObserver = chartObserver;
    this.ExtentX = descriptor.extentLinear().accessors([o => o.Index]);
    this.ExtentY = descriptor.extentLinear().accessors([o => this.GetExtremum(o, -1), o => this.GetExtremum(o, 1)]);

    // Create chart for each series in specified area

    for (let seriesName in series) {

      switch (series[seriesName].ChartType) {

        case ChartControl.ChartType.Bar:

          seriesItems.push(descriptor.autoBandwidth(descriptor
            .seriesCanvasBar()
            .crossValue((o, i) => o.Index)
            .mainValue((o, i) => o.Areas[this.ChartArea].Series[seriesName].Close)));

          break;

        case ChartControl.ChartType.Line:

          seriesItems.push(descriptor
            .seriesCanvasLine()
            .crossValue((o, i) => o.Index)
            .mainValue((o, i) => o.Areas[this.ChartArea].Series[seriesName].Close));

          break;

        case ChartControl.ChartType.Candle:

          seriesItems.push(descriptor.autoBandwidth(descriptor
            .seriesCanvasCandlestick()
            .crossValue((o, i) => o.Index)
            .lowValue((o, i) => o.Areas[this.ChartArea].Series[seriesName].Low)
            .highValue((o, i) => o.Areas[this.ChartArea].Series[seriesName].High)
            .openValue((o, i) => o.Areas[this.ChartArea].Series[seriesName].Open)
            .closeValue((o, i) => o.Areas[this.ChartArea].Series[seriesName].Close))
            .widthFraction(0.5));

          break;

        case ChartControl.ChartType.Area:

          seriesItems.push(descriptor
            .seriesCanvasArea()
            .crossValue((o, i) => o.Index)
            .mainValue((o, i) => o.Areas[this.ChartArea].Series[seriesName].Close));

          break;

        case ChartControl.ChartTransactionType.Deal:

          seriesItems.push(descriptor
            .seriesCanvasPoint()
            .crossValue((o, i) => o.Index)
            .mainValue((o, i) => o.Areas[this.ChartArea].Series[seriesName].Close)
            .size((o, i) => 0)
            .decorate((context, point) => {

              const actions = point.Areas[this.ChartArea].Transactions || [];

              for (let i = 0; i < actions.length; i++) {

                const item = actions[i];
                const closeItem = this.ScaleY(item.Close);
                const closeBase = this.ScaleY(point.Areas[this.ChartArea].Series[seriesName].Close);
                const symbol = item.TransactionType === ChartControl.TransactionType.Sell ? '▿' : '▵';

                context.font = '20px Arial';
                context.fillStyle = '#000';
                context.textAlign = 'center';
                context.textBaseline = 'middle';
                context.fillText(symbol, 0, closeItem - closeBase);
              }
            }));

          break;
      }
    }

    // Annotations

    seriesItems.unshift(descriptor
      .annotationCanvasGridline()
      .xDecorate((ctx, ...args) => {
        ctx.lineWidth = 0.5;
        ctx.strokeStyle = "#EEE";
      })
      .yDecorate((ctx, ...args) => {
        ctx.lineWidth = 0.5;
        ctx.strokeStyle = "#EEE";
      }));

    const annotationLineH = this.annotationLineH = descriptor
      .annotationCanvasLine()
      .xScale(this.ScaleX)
      .yScale(this.ScaleY);

    const annotationLineV = this.annotationLineV = descriptor
      .annotationCanvasLine()
      .orient('vertical')
      .xScale(this.ScaleX)
      .yScale(this.ScaleY);

    const annotationCross = descriptor
      .annotationCanvasCrosshair()
      .xScale(this.ScaleX)
      .yScale(this.ScaleY)
      .xLabel(o => '')
      .yLabel(o => '')
      .xDecorate((ctx, ...args) => {
        ctx.setLineDash([5, 5])
        ctx.lineWidth = 0.5;
        ctx.strokeStyle = "#000";
      })
      .yDecorate((ctx, ...args) => {
        ctx.setLineDash([5, 5])
        ctx.lineWidth = 0.5;
        ctx.strokeStyle = "#000";
      })
      .decorate(ctx => {
        ctx.scale(0, 0);
      });

    seriesItems.unshift(annotationLineH);
    seriesItems.unshift(annotationLineV);
    seriesItems.push(annotationCross);

    // Series combination

    const chartGroup = d3.select('#' + this.ChartArea + ' .chart-group');
    const chartView = chartGroup.select('.plot-area');
    const chartContext = chartView.select('canvas').node().getContext('2d');
    const charts = descriptor
      .seriesCanvasMulti()
      .series(seriesItems)
      .context(chartContext)
      .xScale(this.ScaleX)
      .yScale(this.ScaleY)
      .mapping((data, index, series) => {

        switch (series[index]) {
          case annotationCross: return this.CrossLines;
          case annotationLineV: return this.LevelsX;
          case annotationLineH: return this.LevelsY;
        }

        return this.DataBounds;
      });

    const callout = descriptor.dataJoin('g', 'tick callout');
    const axisContainerX = chartGroup.select('.axis-x-bottom');
    const axisContainerY = chartGroup.select('.axis-y-right');

    const axisX = descriptor
      .axisBottom(this.ScaleX)
      .ticks(5)
      .tickFormat((index, i) => this.ComposeX((this.DataPoints[index] || this.DataPoints[0] || {}).Time));

    const axisY = descriptor
      .axisRight(this.ScaleY)
      .ticks(5)
      .tickFormat((o, i) => this.ComposeY(o));

    this.chartContainer = chartGroup.on('draw', () => {

      this.ScaleX.range([0, chartView.node().clientWidth]);
      this.ScaleY.range([chartView.node().clientHeight, 0]);

      chartContext.scale(window.devicePixelRatio, window.devicePixelRatio);
      charts(this.DataBounds);

      d3.select(axisContainerX.node()).select('svg').call(axisX);
      d3.select(axisContainerY.node()).select('svg').call(axisY);

      if (this.CrossLines.length) {
        this.CreateCrossLabel('X', callout, axisContainerX.select('svg'));
        this.CreateCrossLabel('Y', callout, axisContainerY.select('svg'));
      }
    });

    // Cross

    this.pointer = descriptor
      .pointer()
      .on('point', event => {
        this.CrossLines = event.length ? [{ x: event[0].x, y: event[0].y }] : [];
        this.Render();
      });

    // Events

    d3
      .select('#' + this.ChartArea)
      .select('.axis-x')
      .call(this.OnDragAxisX);

    d3
      .select('#' + this.ChartArea)
      .select('.axis-y')
      .call(this.OnDragAxisY);

    d3
      .select('#' + this.ChartArea)
      .select('.plot-area')
      .call(this.OnMouse);

    // Subscribe to external updates

    chartObserver
      .events('Sync')[this.ChartArea] = (iDomainX, iDomainY, iCount) =>
        this.Scale(iDomainX, null, iCount, this.ChartArea);
  }

  /**
   * Generic mouse event handler
   */
  OnMouse = d3.zoom().on('zoom', (event, items) => {

    const e = event.sourceEvent || {};

    if (e.type === 'wheel') {
      e.shiftKey ?
        this.OnZoomX(e.deltaY, 3) :
        this.OnPanX(e.deltaY, 3);
    }

    if (e.type === 'mousemove') {
      this.OnPanX(e.x - this.MoveX, 1);
      this.MoveX = e.x;
    }
  });

  /**
   * Pan along X axis using mouse wheel
   * @param {any} delta
   * @param {any} deltaSize
   */
  OnZoomX(delta, deltaSize) {

    let count = this.DataBounds.length;
    let domainX = this.GetState().domainX;

    // Scale up

    if (delta < 0 && count > 0) {
      domainX[0] += deltaSize;
      domainX[1] -= deltaSize;
    }

    // Scale down

    if (delta > 0 && count < this.DataPoints.length) {
      domainX[0] = Math.max(domainX[0] - deltaSize, 0);
      domainX[1] = Math.min(domainX[1] + deltaSize, this.DataPoints.length - 1);
    }

    this.Scale(domainX, null, domainX[1] - domainX[0]);
  }

  /**
   * Pan along X axis using mouse wheel
   * @param {any} delta
   * @param {any} deltaSize
   */
  OnPanX(delta, deltaSize) {

    const domainX = this.GetState().domainX;

    if (delta < 0) {
      domainX[0] += deltaSize;
      domainX[1] += deltaSize;
    }

    if (delta > 0) {
      domainX[0] -= deltaSize;
      domainX[1] -= deltaSize;
    }

    this.Scale(domainX);
  }

  /**
   * Drag event handle for X axis
   */
  OnDragAxisX = d3.drag().on('drag', (event, items) => {
    this.OnZoomX(-event.dx, 1);
  })

  /**
   * Drag event handle for Y axis
   */
  OnDragAxisY = d3.drag().on('drag', (event, items) => {

    const domainY = this.ScaleY.domain();
    const deltaY = event.dy;
    const stepY = Math.abs(domainY[1] - domainY[0]) * 0.05;

    if (deltaY > 0) {
      domainY[0] = domainY[0] - stepY;
      domainY[1] = domainY[1] + stepY;
    }

    if (deltaY < 0) {
      domainY[0] = domainY[0] + stepY;
      domainY[1] = domainY[1] - stepY;
    }

    if (domainY[1] > domainY[0]) {
      this.Scale(null, domainY);
    }
  })

  /**
   * Create dynamic tick with label
   * @param {any} orientation
   * @param {any} callout
   * @param {any} axisElement
   */
  CreateCrossLabel(orientation, callout, axisElement) {

    let pos = null;
    let value = null;
    let calloutItem = null;

    switch (orientation) {

      case 'X':

        pos = this.CrossLines[0].x || 0;

        const point =
          this.DataPoints[Math.floor(this.ScaleX.invert(pos))] ||
          this.DataPoints[this.DataPoints.length - 1];

        value = (point || {}).Time || 0;
        calloutItem = callout(axisElement, [value]).attr('transform', o => 'translate(' + pos + ', 0)');

        calloutItem
          .enter()
          .append('rect')
          .attr('fill', o => '#45526e')
          .attr('width', o => '100')
          .attr('height', o => '15')
          .attr('transform', o => 'translate(-50, 5)');

        calloutItem
          .enter()
          .append('text')
          .text(this.ComposeX(value))
          .attr('fill', o => '#FFF')
          .attr('transform', o => 'translate(0, 16)');

        break;

      case 'Y':

        pos = this.CrossLines[0].y || 0;
        value = this.ScaleY.invert(pos) || 0;
        calloutItem = callout(axisElement, [value]).attr('transform', o => 'translate(0, ' + pos + ')');

        calloutItem
          .enter()
          .append('rect')
          .attr('fill', o => '#45526e')
          .attr('width', o => '50')
          .attr('height', o => '15')
          .attr('transform', o => 'translate(0, -8)');

        calloutItem
          .enter()
          .append('text')
          .text(value.toFixed(2))
          .attr('fill', o => '#FFF')
          .attr('transform', o => 'translate(9, 3)');

        break;
    }
  }

  /**
   * Find min-max values inside extent function
   * @param {any} point
   * @param {any} direction
   */
  GetExtremum(point, direction) {

    let min = 1000000000;
    let max = -1000000000;
    let extremum = direction;

    for (let i in point.Areas[this.ChartArea].Series) {

      const series = point.Areas[this.ChartArea].Series[i] || {};

      // Calculate min and max based on valid inputs only and exclude 0 series, like position marks

      if (ChartControl.ChartType[(series.Chart || {}).ChartType]) {
        min = Math.min(min, series.Low || series.Close);
        max = Math.max(max, series.High || series.Close);
        extremum = direction > 0 ? min : max;
      }
    }

    return extremum === direction ? null : extremum;
  }

  /**
    * Set domains for one or all axes
    * @param {any} domainX
    * @param {any} domainY
    * @param {any} visibleCount
    * @param {any} observer
    */
  Scale(domainX = null, domainY = null, visibleCount = 0, observer = null) {

    if (domainX) {

      const minX = domainX[0];
      const maxX = domainX[1];
      const count = Math.max(maxX - Math.max(minX, 0), 0);
      const extension = Math.max(visibleCount - count, 0);

      this.DomainX = domainX;
      this.DataBounds = this.DataPoints.slice(Math.max(minX, 0), maxX + 1);
      this.ScaleX.domain([minX - extension, maxX + 1]);
    }

    this.ScaleY.domain(this.DomainY = domainY || this.GetState().domainY);
    this.Render();

    // Do not scale if it's a response to an external notification

    if (observer === null) {
      this.ChartObserver.send('Sync', this.ChartArea, domainX, domainY, visibleCount);
    }
  }

  /**
   * Format X values
   * @param {any} seconds
   */
  ComposeX(seconds = null) {

    if (seconds) {

      const date = new Date(seconds);
      const pad = i => i < 10 ? ('0' + i) : ('' + i);

      const days = [
        date.getFullYear(),
        pad(date.getMonth() + 1),
        pad(date.getDate())];

      const hours = [
        pad(date.getHours()),
        pad(date.getMinutes())];

      return days.join('-') + ' ' + hours.join(':');
    }

    return null;
  }

  /**
   * Format Y values
   * @param {any} input
   */
  ComposeY(input = null) {

    if (input) {

      const number = +input;

      if (number >= 1000000) {
        return (number / 1000000).toFixed(2) + ' M ';
      }

      if (number < 100000) {
        return number.toFixed(2);
      }

      return number.toFixed(0);
    }

    return null;
  }

  /**
   * Get the most important information for visible area in specified domain range
   */
  GetState() {

    const state = {};

    state.domainX = this.DomainX;
    state.domainY = this.ExtentY(this.DataBounds);

    // Make sure to have valid bounds

    state.domainX[0] = state.domainX[0] || 0;
    state.domainX[1] = state.domainX[1] || 0;
    state.domainY[0] = state.domainY[0] || 0;
    state.domainY[1] = state.domainY[1] || 0;

    // Add 10 as a random value to expand the grid and make it look better

    if (state.domainY[0] === state.domainY[1]) {
      state.domainY[0] -= 10;
      state.domainY[1] += 10;
    }

    return state;
  }

  /**
   * Update horizontal levels
   * @param {any} levels
   */
  UpdateLevels(levels) {
    this.LevelsY = levels;
    this.Render();
  }

  /**
   * Initiate chart invalidate 
   */
  Render() {

    this
      .chartContainer
      .node()
      .requestRedraw();

    d3
      .select('#' + this.ChartArea + ' .plot-area')
      .call(this.pointer);
  }
}

/**
 * Namespace for interop calls
 */
window.ChartFunctions = window.ChartFunctions || {

  /**
   * Reference to synced chart areas and data points
   * @param {any} message
   */
  Control: {},

  /**
   * Define chart structure, e.g. areas and related series
   * @param {any} message
   */
  CreateCharts: (id, message) => {

    const areas = new Map();
    const inputs = JSON.parse(message) || [];

    // Group series by areas

    for (let i = 0; i < inputs.length; i++) {

      const areaName = inputs[i].ChartArea;

      areas[areaName] = areas[areaName] || {};
      areas[areaName][inputs[i].Name] = inputs[i];
    }

    // Create charts

    window.ChartFunctions.Control = new ChartData(fc, areas);
  },

  /**
   * Reset charts
   * @param {any} message
   */
  DeleteCharts: (id) => {

    delete window.ChartFunctions.Control;

    const canvasAreas = document.querySelectorAll('canvas');

    for (let i = 0; i < canvasAreas.length; i++) {
      canvasAreas[i].getContext('2d').clearRect(0, 0, canvasAreas[i].width, canvasAreas[i].height);
      canvasAreas[i].width = canvasAreas[i].width;
    }
  },

  /**
   * Update or create data points
   * @param {any} message
   */
  UpdatePoints: (message) => {
    window.ChartFunctions.Control.UpdatePoints(JSON.parse(message) || []);
  },

  /**
   * Update transactions on the chart
   * @param {any} message
   */
  UpdateTransactions: (message) => {

    const chart = window.ChartFunctions.Control;
    const points = (JSON.parse(message) || []).map(o => ({ ...o, Transactions: [o] }));

    chart.UpdatePoints(points);
  },

  /**
   * Update order levels on the chart
   * @param {any} message
   */
  UpdateLevels: (message) => {

    const areas = {};
    const levels = JSON.parse(message) || [];
    const chart = window.ChartFunctions.Control;

    for (let i = 0; i < levels.length; i++) {
      areas[levels[i].Chart.ChartArea] = areas[levels[i].Chart.ChartArea] || [];
      areas[levels[i].Chart.ChartArea].push(levels[i].Close);
    }

    for (let i in chart.Areas) {
      chart.Areas[i].Instance.UpdateLevels(areas[i]);
    }
  }
}
