using Flurl;
using System.Collections;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Terminal.Core.Enums;
using Terminal.Core.Models;
using Tradier.Mappers;
using Tradier.Messages.Trading;

namespace Tradier
{
  public partial class Adapter
  {
    /// <summary>
    /// Place an order to trade a single option
    /// </summary>
    public async Task<OrderResponseMessage> SendOptionOrder(OrderModel order, bool preview = false)
    {
      var data = new Hashtable
      {
        { "class", "option" },
        { "account_id", Account.Descriptor },
        { "symbol", order?.Instrument?.Basis?.Name ?? order.Instrument.Name },
        { "option_symbol", GetOptionName(order) },
        { "side", Upstream.GetSide(order) },
        { "quantity", order.Amount },
        { "type", Upstream.GetOrderType(order.Type) },
        { "duration", Upstream.GetTimeSpan(order.TimeSpan) },
        { "price", order.OpenPrice },
        { "stop", order.ActivationPrice ?? order.OpenPrice },
        { "tag", order.Descriptor },
        { "preview", preview }
      };

      var source = $"{DataUri}/accounts/{Account.Descriptor}/orders".SetQueryParams(data);
      var response = await Send<OrderResponseCoreMessage>(source, HttpMethod.Post);

      return response.OrderReponse;
    }

    /// <summary>
    /// Place a multileg order with up to 4 legs
    /// </summary>
    public async Task<OrderResponseMessage> SendGroupOrder(OrderModel order, bool preview = false)
    {
      var index = 0;
      var data = new Hashtable
      {
        { "class", "multileg" },
        { "symbol", order?.Instrument?.Basis?.Name ?? order.Instrument.Name },
        { "type", Upstream.GetOrderType(order.Type) },
        { "duration", Upstream.GetTimeSpan(order.TimeSpan) },
        { "price", order.OpenPrice },
        { "tag", order.Descriptor }
      };

      foreach (var item in order.Orders)
      {
        data[$"option_symbol[{index}]"] = GetOptionName(item);
        data[$"side[{index}]"] = Upstream.GetSide(item);
        data[$"quantity[{index}]"] = item.Amount;

        index++;
      }

      var source = $"{DataUri}/accounts/{Account.Descriptor}/orders".SetQueryParams(data);
      var response = await Send<OrderResponseCoreMessage>(source, HttpMethod.Post);

      return response.OrderReponse;
    }

    /// <summary>
    /// Place an order to trade an equity security
    /// </summary>
    public async Task<OrderResponseMessage> SendEquityOrder(OrderModel order, bool preview = false)
    {
      var data = new Hashtable
      {
        { "account_id", Account.Descriptor },
        { "class", "equity" },
        { "symbol", order?.Instrument?.Basis?.Name ?? order.Instrument.Name },
        { "side", Upstream.GetSide(order) },
        { "quantity", order.Amount},
        { "type", Upstream.GetOrderType(order.Type) },
        { "duration", Upstream.GetTimeSpan(order.TimeSpan) },
        { "price", order.OpenPrice },
        { "stop", order.ActivationPrice ?? order.OpenPrice },
        { "preview", preview },
        { "tag", order.Descriptor }
      };

      var source = $"{DataUri}/accounts/{Account.Descriptor}/orders".SetQueryParams(data);
      var response = await Send<OrderResponseCoreMessage>(source, HttpMethod.Post);

      return response.OrderReponse;
    }

    /// <summary>
    /// Place a combo order. This is a specialized type of order consisting of one equity leg and one option leg
    /// </summary>
    public async Task<OrderResponseMessage> SendComboOrder(OrderModel order, bool preview = false)
    {
      var index = 0;
      var data = new Hashtable
      {
        { "class", "combo" },
        { "symbol", order?.Instrument?.Basis?.Name ?? order.Instrument.Name },
        { "type", Upstream.GetOrderType(order.Type) },
        { "duration", Upstream.GetTimeSpan(order.TimeSpan) },
        { "price", order.OpenPrice },
        { "tag", order.Descriptor }
      };

      foreach (var item in order.Orders)
      {
        data[$"option_symbol[{index}]"] = GetOptionName(item);
        data[$"side[{index}]"] = Upstream.GetSide(item);
        data[$"quantity[{index}]"] = item.Amount;

        index++;
      }

      var source = $"{DataUri}/accounts/{Account.Descriptor}/orders".SetQueryParams(data) ;
      var response = await Send<OrderResponseCoreMessage>(source, HttpMethod.Post);

      return response.OrderReponse;
    }

    /// <summary>
    /// Place a one-triggers-other order. This order type is composed of two separate orders sent simultaneously
    /// </summary>
    public async Task<OrderResponseMessage> SendOtoOrder(OrderModel order, bool preview = false)
    {
      var index = 0;
      var data = new Hashtable
      {
        { "class", "oto" },
        { "duration", Upstream.GetTimeSpan(order.TimeSpan) },
        { "preview", preview },
        { "tag", order.Descriptor }
      };

      var subOrders = order
        .Orders
        .Where(o => o.Instruction is InstructionEnum.Brace)
        .Prepend(order);

      foreach (var item in subOrders)
      {
        data[$"symbol[{index}]"] = item?.Instrument?.Basis?.Name ?? item.Instrument.Name;
        data[$"quantity[{index}]"] = item.Amount;
        data[$"type[{index}]"] = Upstream.GetOrderType(item.Type);
        data[$"option_symbol[{index}]"] = GetOptionName(item);
        data[$"side[{index}]"] = Upstream.GetSide(order);
        data[$"price[{index}]"] = item.OpenPrice;
        data[$"stop[{index}]"] = item.ActivationPrice ?? item.OpenPrice;

        index++;
      }

      var source = $"{DataUri}/accounts/{Account.Descriptor}/orders".SetQueryParams(data);
      var response = await Send<OrderResponseCoreMessage>(source, HttpMethod.Post);

      return response.OrderReponse;
    }

    /// <summary>
    /// Place a one-cancels-other order. This order type is composed of two separate orders sent simultaneously
    /// </summary>
    public async Task<OrderResponseMessage> SendOcoOrder(OrderModel order, bool preview = false)
    {
      var index = 0;
      var data = new Hashtable
      {
        { "class", "oco" },
        { "duration", Upstream.GetTimeSpan(order.TimeSpan) },
        { "preview", preview },
        { "tag", order.Descriptor }
      };

      var subOrders = order
        .Orders
        .Where(o => o.Instruction is InstructionEnum.Brace)
        .Prepend(order);

      foreach (var item in subOrders)
      {
        data[$"symbol[{index}]"] = item?.Instrument?.Basis?.Name ?? item.Instrument.Name;
        data[$"quantity[{index}]"] = item.Amount;
        data[$"type[{index}]"] = Upstream.GetOrderType(item.Type);
        data[$"option_symbol[{index}]"] = GetOptionName(item);
        data[$"side[{index}]"] = Upstream.GetSide(order);
        data[$"price[{index}]"] = item.OpenPrice;
        data[$"stop[{index}]"] = item.ActivationPrice ?? item.OpenPrice;

        index++;
      }

      var source = $"{DataUri}/accounts/{Account.Descriptor}/orders".SetQueryParams(data);
      var response = await Send<OrderResponseCoreMessage>(source, HttpMethod.Post);

      return response.OrderReponse;
    }

    /// <summary>
    /// Place a one-triggers-one-cancels-other order. This order type is composed of three separate orders sent simultaneously
    /// </summary>
    public async Task<OrderResponseMessage> SendBraceOrder(OrderModel order, bool preview = false)
    {
      var index = 0;
      var data = new Hashtable
      {
        { "class", "otoco" },
        { "duration", Upstream.GetTimeSpan(order.TimeSpan) },
        { "preview", preview },
        { "tag", order.Descriptor }
      };

      var subOrders = order
        .Orders
        .Where(o => o.Instruction is InstructionEnum.Brace)
        .Prepend(order);

      foreach (var item in subOrders)
      {
        data[$"symbol[{index}]"] = item?.Instrument?.Basis?.Name ?? item.Instrument.Name;
        data[$"quantity[{index}]"] = item.Amount;
        data[$"type[{index}]"] = Upstream.GetOrderType(item.Type);
        data[$"option_symbol[{index}]"] = GetOptionName(item);
        data[$"side[{index}]"] = Upstream.GetSide(item);
        data[$"price[{index}]"] = item.OpenPrice;
        data[$"stop[{index}]"] = item.ActivationPrice ?? item.OpenPrice;

        index++;
      }

      var source = $"{DataUri}/accounts/{Account.Descriptor}/orders".SetQueryParams(data);
      var response = await Send<OrderResponseCoreMessage>(source, HttpMethod.Post);

      return response.OrderReponse;
    }

    /// <summary>
    /// Modify an order. You may change some or all of these parameters.
    /// </summary>
    public async Task<OrderResponseMessage> UpdateOrder(string orderId, string type = null, string duration = null, double? price = null, double? stop = null)
    {
      var data = new Hashtable
      {
        { "type", type },
        { "duration", duration },
        { "price", price },
        { "stop", stop }
      };

      var source = $"{DataUri}/accounts/{Account.Descriptor}/orders/{orderId}".SetQueryParams(data);
      var response = await Send<OrderResponseCoreMessage>(source, HttpMethod.Put);

      return response.OrderReponse;
    }

    /// <summary>
    /// Cancel an order using the default account number
    /// </summary>
    public virtual async Task<OrderResponseMessage> ClearOrder(string orderId)
    {
      var source = $"{DataUri}/accounts/{Account.Descriptor}/orders/{orderId}";
      var response = await Send<OrderResponseCoreMessage>(source, HttpMethod.Delete);

      return response.OrderReponse;
    }

    /// <summary>
    /// Return name only for options
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual string GetOptionName(OrderModel order)
    {
      switch (order.Instrument.Type)
      {
        case InstrumentEnum.Options: 
        case InstrumentEnum.FutureOptions: return order.Instrument.Name;
      }

      return null;
    }
  }
}
