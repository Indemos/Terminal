using Distribution.DomainSpace;
using FluentValidation.Results;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Terminal.Core.EnumSpace;
using Terminal.Core.ExtensionSpace;
using Terminal.Core.MessageSpace;
using Terminal.Core.ServiceSpace;
using Terminal.Core.ValidatorSpace;

namespace Terminal.Core.ModelSpace
{
  /// <summary>
  /// Interface that defines input and output processes
  /// </summary>
  public interface IConnectorModel : IBaseModel, IDisposable
  {
    /// <summary>
    /// Production or Development mode
    /// </summary>
    EnvironmentEnum Mode { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    IAccountModel Account { get; set; }

    /// <summary>
    /// Send order event
    /// </summary>
    ISubject<ITransactionMessage<ITransactionOrderModel>> OrderStream { get; }

    /// <summary>
    /// Incoming data event
    /// </summary>
    ISubject<ITransactionMessage<IPointModel>> DataStream { get; }

    /// <summary>
    /// Restore state and initialize
    /// </summary>
    Task Connect();

    /// <summary>
    /// Save state and dispose
    /// </summary>
    Task Disconnect();

    /// <summary>
    /// Continue execution
    /// </summary>
    Task Subscribe();

    /// <summary>
    /// Suspend execution
    /// </summary>
    Task Unsubscribe();
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public abstract class ConnectorModel : BaseModel, IConnectorModel
  {
    /// <summary>
    /// Production or Sandbox
    /// </summary>
    public virtual EnvironmentEnum Mode { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    public virtual IAccountModel Account { get; set; }

    /// <summary>
    /// Incoming data event
    /// </summary>
    public virtual ISubject<ITransactionMessage<IPointModel>> DataStream { get; }

    /// <summary>
    /// Send order event
    /// </summary>
    public virtual ISubject<ITransactionMessage<ITransactionOrderModel>> OrderStream { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ConnectorModel()
    {
      Mode = EnvironmentEnum.Sandbox;
      DataStream = new Subject<ITransactionMessage<IPointModel>>();
      OrderStream = new Subject<ITransactionMessage<ITransactionOrderModel>>();
    }

    /// <summary>
    /// Restore state and initialize
    /// </summary>
    public abstract Task Connect();

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public abstract Task Disconnect();

    /// <summary>
    /// Suspend execution
    /// </summary>
    public abstract Task Unsubscribe();

    /// <summary>
    /// Continue execution
    /// </summary>
    public abstract Task Subscribe();

    /// <summary>
    /// Dispose
    /// </summary>
    public abstract void Dispose();

    /// <summary>
    /// Ensure that each series has a name and can be attached to specific area on the chart
    /// </summary>
    /// <param name="orders"></param>
    protected bool ValidateOrders(params ITransactionOrderModel[] orders)
    {
      var errors = new List<ValidationFailure>();
      var orderRules = InstanceService<TransactionOrderPriceValidator>.Instance;
      var instrumentRules = InstanceService<InstrumentCollectionsValidator>.Instance;

      foreach (var model in orders)
      {
        errors.AddRange(orderRules.Validate(model).Errors);
        errors.AddRange(instrumentRules.Validate(model.Instrument).Errors);
        errors.AddRange(model.Orders.SelectMany(o => orderRules.Validate(o).Errors));
        errors.AddRange(model.Orders.SelectMany(o => instrumentRules.Validate(o.Instrument).Errors));
      }

      foreach (var error in errors)
      {
        InstanceService<LogService>.Instance.Log.Error(error.ErrorMessage);
      }

      return errors.Any() is false;
    }

    /// <summary>
    /// Get next available point
    /// </summary>
    /// <returns></returns>
    protected virtual IPointModel GetPoint(IDictionary<string, StreamReader> streams, IDictionary<string, IPointModel> points)
    {
      var index = string.Empty;

      foreach (var stream in streams)
      {
        points.TryGetValue(stream.Key, out IPointModel point);

        if (point is null)
        {
          var input = stream.Value.ReadLine();

          if (string.IsNullOrEmpty(input) is false)
          {
            points[stream.Key] = Parse(stream.Key, input);
          }
        }

        points.TryGetValue(index, out IPointModel min);
        points.TryGetValue(stream.Key, out IPointModel current);

        var isOne = string.IsNullOrEmpty(index);
        var isMin = current is not null && min is not null && current.Time <= min.Time;

        if (isOne || isMin)
        {
          index = stream.Key;
        }
      }

      var response = points[index];

      points[index] = null;

      return response;
    }

    /// <summary>
    /// Parse point
    /// </summary>
    /// <param name="name"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    protected virtual IPointModel Parse(string name, string input)
    {
      var props = input.Split(" ");

      long.TryParse(props.ElementAtOrDefault(0), out long date);

      if (date is 0)
      {
        return null;
      }

      double.TryParse(props.ElementAtOrDefault(1), out double bid);
      double.TryParse(props.ElementAtOrDefault(2), out double bidSize);
      double.TryParse(props.ElementAtOrDefault(3), out double ask);
      double.TryParse(props.ElementAtOrDefault(4), out double askSize);

      var response = new PointModel
      {
        Name = name,
        Time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(date),
        Ask = ask,
        Bid = bid,
        Last = ask,
        AskSize = askSize,
        BidSize = bidSize
      };

      if (askSize.IsEqual(0))
      {
        response.Last = bid;
      }

      return response;
    }

    /// <summary>
    /// Define open price based on order
    /// </summary>
    /// <param name="nextOrder"></param>
    protected virtual IList<ITransactionOrderModel> GetOpenPrices(ITransactionOrderModel nextOrder)
    {
      var openPrice = nextOrder.Price;
      var pointModel = nextOrder.Instrument.PointGroups.LastOrDefault();

      if (openPrice is null)
      {
        switch (nextOrder.Side)
        {
          case OrderSideEnum.Buy: openPrice = pointModel.Ask; break;
          case OrderSideEnum.Sell: openPrice = pointModel.Bid; break;
        }
      }

      return new List<ITransactionOrderModel>
      {
        new TransactionOrderModel
        {
          Price = openPrice,
          Size = nextOrder.Size,
          Time = pointModel.Time
        }
      };
    }

    /// <summary>
    /// Update points
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected virtual IPointModel UpdatePoints(IPointModel point)
    {
      var instrument = Account.Instruments[point.Name];
      var estimates = Account.ActivePositions.Select(o => o.Value.GainLossEstimate).ToList();

      point.Account = Account;
      point.Instrument = instrument;
      point.TimeFrame = instrument.TimeFrame;

      instrument.Points.Add(point);
      instrument.PointGroups.Add(point, instrument.TimeFrame);

      return point;
    }
  }
}
