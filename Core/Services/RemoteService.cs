using Core.ManagerSpace;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Core.ModelSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  public interface IRemoteService : IDisposable
  {
    /// <summary>
    /// Single instance
    /// </summary>
    HttpClient Client { get; }

    /// <summary>
    /// Send GET request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="queryItems"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    Task<T> Get<T>(
      string source,
      IDictionary<dynamic, dynamic> queryItems = null,
      CancellationTokenSource cts = null) where T : new();

    /// <summary>
    /// Send POST request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="queryItems"></param>
    /// <param name="content"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    Task<T> Post<T>(
      string source,
      IDictionary<dynamic, dynamic> queryItems = null,
      HttpContent content = null,
      CancellationTokenSource cts = null) where T : new();
  }

  /// <summary>
  /// Service to track account changes, including equity and quotes
  /// </summary>
  public class RemoteService : IRemoteService
  {
    /// <summary>
    /// HTTP client instance
    /// </summary>
    protected HttpClient _client = new HttpClient();

    /// <summary>
    /// Logger instance
    /// </summary>
    public HttpClient Client => _client;

    /// <summary>
    /// Send GET request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="queryItems"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    public async Task<T> Get<T>(
      string source,
      IDictionary<dynamic, dynamic> queryItems = null,
      CancellationTokenSource cts = null) where T : new()
    {
      var response = await Send(source, queryItems, HttpMethod.Get, null, cts);

      if (response == null)
      {
        return default;
      }

      return ConversionManager.Deserialize<T>(response);
    }

    /// <summary>
    /// Send POST request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="queryItems"></param>
    /// <param name="content"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    public async Task<T> Post<T>(
      string source,
      IDictionary<dynamic, dynamic> queryItems = null,
      HttpContent content = null,
      CancellationTokenSource cts = null) where T : new()
    {
      var response = await Send(source, queryItems, HttpMethod.Post, content, cts);

      if (response == null)
      {
        return default;
      }
      
      return ConversionManager.Deserialize<T>(response);
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
      _client.Dispose();
    }

    /// <summary>
    /// Generic query sender
    /// </summary>
    /// <param name="source"></param>
    /// <param name="queryItems"></param>
    /// <param name="queryType"></param>
    /// <param name="queryHeaders"></param>
    /// <param name="content"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    protected async Task<string> Send(
      string source,
      IDictionary<dynamic, dynamic> queryItems = null,
      HttpMethod queryType = null,
      HttpContent content = null,
      CancellationTokenSource cts = null)
    {
      var cancellation = cts == null ? default : cts.Token;
      var queryParams = HttpUtility.ParseQueryString(string.Empty);
      var query = string.Empty;

      if (queryItems != null)
      {
        foreach (var item in queryItems)
        {
          queryParams.Add(item.Key, item.Value);
        }

        query = queryParams.ToString();
      }

      var message = new HttpRequestMessage
      {
        Method = queryType ?? HttpMethod.Get,
        RequestUri = new Uri(source + query),
        Content = content
      };

      var response = await _client.SendAsync(message, cancellation);
      
      if (response.IsSuccessStatusCode)
      {
        return await response.Content.ReadAsStringAsync();
      }

      return null;
    }
  }
}
