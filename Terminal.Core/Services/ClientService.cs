using Terminal.Core.ExtensionSpace;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Terminal.Core.ServiceSpace
{
  /// <summary>
  /// HTTP service
  /// </summary>
  public interface IClientService : IDisposable
  {
    /// <summary>
    /// Max execution time
    /// </summary>
    TimeSpan Timeout { get; set; }

    /// <summary>
    /// Instance
    /// </summary>
    HttpClient Client { get; }

    /// <summary>
    /// Send GET request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="query"></param>
    /// <param name="headers"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    Task<T> Get<T>(
      string source,
      IDictionary<dynamic, dynamic> query = null,
      IDictionary<dynamic, dynamic> headers = null,
      CancellationTokenSource cts = null);

    /// <summary>
    /// Send POST request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="query"></param>
    /// <param name="headers"></param>
    /// <param name="content"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    Task<T> Post<T>(
      string source,
      IDictionary<dynamic, dynamic> query = null,
      IDictionary<dynamic, dynamic> headers = null,
      HttpContent content = null,
      CancellationTokenSource cts = null);

    /// <summary>
    /// Stream HTTP content
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="query"></param>
    /// <param name="headers"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    Task<Stream> Stream(
      string source,
      IDictionary<dynamic, dynamic> query = null,
      IDictionary<dynamic, dynamic> headers = null,
      CancellationTokenSource cts = null);
  }

  /// <summary>
  /// Service to track account changes, including equity and quotes
  /// </summary>
  public class ClientService : IClientService
  {
    /// <summary>
    /// Max execution time
    /// </summary>
    public virtual TimeSpan Timeout { get; set; }

    /// <summary>
    /// HTTP client instance
    /// </summary>
    public virtual HttpClient Client { get; protected set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ClientService()
    {
      Timeout = TimeSpan.FromSeconds(5);
      Client = InstanceService<HttpClient>.Instance;
    }

    /// <summary>
    /// Send GET request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="query"></param>
    /// <param name="headers"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    public virtual async Task<T> Get<T>(
      string source,
      IDictionary<dynamic, dynamic> query = null,
      IDictionary<dynamic, dynamic> headers = null,
      CancellationTokenSource cts = null)
    {
      return await (await Send(HttpMethod.Get, source, query, headers, null, cts).ConfigureAwait(false)).DeserializeAsync<T>();
    }

    /// <summary>
    /// Send POST request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="query"></param>
    /// <param name="headers"></param>
    /// <param name="content"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    public virtual async Task<T> Post<T>(
      string source,
      IDictionary<dynamic, dynamic> query = null,
      IDictionary<dynamic, dynamic> headers = null,
      HttpContent content = null,
      CancellationTokenSource cts = null)
    {
      return await (await Send(HttpMethod.Post, source, query, headers, content, cts).ConfigureAwait(false)).DeserializeAsync<T>();
    }

    /// <summary>
    /// Stream HTTP content
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="query"></param>
    /// <param name="headers"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    public virtual async Task<Stream> Stream(
      string source,
      IDictionary<dynamic, dynamic> query = null,
      IDictionary<dynamic, dynamic> headers = null,
      CancellationTokenSource cts = null)
    {
      using (var client = new HttpClient())
      {
        var cancellation = cts is null ? CancellationToken.None : cts.Token;

        if (headers is IEnumerable)
        {
          foreach (var item in headers)
          {
            client.DefaultRequestHeaders.Add($"{ item.Key }", $"{ item.Value }");
          }
        }

        return await client
          .GetStreamAsync(source + "?" + GetQuery(query), cancellation)
          .ConfigureAwait(false);
      }
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose()
    {
      Client.Dispose();
    }

    /// <summary>
    /// Generic query sender
    /// </summary>
    /// <param name="queryType"></param>
    /// <param name="source"></param>
    /// <param name="query"></param>
    /// <param name="content"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    protected async Task<Stream> Send(
      HttpMethod queryType,
      string source,
      IDictionary<dynamic, dynamic> query = null,
      IDictionary<dynamic, dynamic> headers = null,
      HttpContent content = null,
      CancellationTokenSource cts = null)
    {
      Client.Timeout = Timeout;

      var message = new HttpRequestMessage
      {
        Content = content,
        Method = queryType,
        RequestUri = new Uri(source + "?" + GetQuery(query))
      };

      if (headers is IEnumerable)
      {
        foreach (var item in headers)
        {
          message.Headers.Add($"{ item.Key }", $"{ item.Value }");
        }
      }

      if (cts is null)
      {
        cts = new CancellationTokenSource(Timeout);
      }

      HttpResponseMessage response = null;

      try
      {
        response = await Client
          .SendAsync(message, cts.Token)
          .ConfigureAwait(false);
      }
      catch (Exception e)
      {
        InstanceService<LogService>.Instance.Log.Error(e.Message);
        return null;
      }

      return await response
        .Content
        .ReadAsStreamAsync(cts.Token)
        .ConfigureAwait(false);
    }

    /// <summary>
    /// Convert dictionary to URL params
    /// </summary>
    /// <param name="inputs"></param>
    /// <returns></returns>
    protected string GetQuery(IDictionary<dynamic, dynamic> query)
    {
      var inputs = HttpUtility.ParseQueryString(string.Empty);

      if (query is IEnumerable)
      {
        foreach (var item in query)
        {
          inputs.Add($"{ item.Key }", $"{ item.Value }");
        }
      }

      return $"{ inputs }";
    }
  }
}
