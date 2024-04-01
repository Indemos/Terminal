using Distribution.Stream;
using Distribution.Services;
using Microsoft.AspNetCore.Http.Extensions;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Terminal.Core.Extensions;
using Terminal.Gateway.Ameritrade.Models;

namespace Ameritrade
{
  public class Authenticator
  {
    /// <summary>
    /// Keys
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Load keys
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public T GetUser<T>(string name)
    {
      var location = Path.Combine(Location, name);

      if (File.Exists(location))
      {
        return File.ReadAllText(location).Deserialize<T>();
      }

      return default;
    }

    /// <summary>
    /// Save keys
    /// </summary>
    /// <param name="name"></param>
    /// <param name="model"></param>
    public void SaveUser<T>(string name, T model)
    {
      File.WriteAllText(Path.Combine(Location, name), JsonSerializer.Serialize(model));
    }

    /// <summary>
    /// Manual sign in 
    /// </summary>
    /// <param name="adapter"></param>
    /// <returns></returns>
    public async Task<UserModel> SignIn(Adapter adapter)
    {
      var user = GetUser<UserModel>(nameof(Authenticator));

      if (user.ExpirationDate > DateTime.UtcNow.Ticks)
      {
        return user;
      }

      using (var browserFetcher = new BrowserFetcher())
      {
        await browserFetcher.DownloadAsync();

        var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        var source = new UriBuilder(adapter.SignInRemoteUri);
        var page = await browser.NewPageAsync();
        var nav = page.WaitForNavigationAsync();

        source.Query = new QueryBuilder
        {
          { "response_type", "code" },
          { "redirect_uri", adapter.SignInLocalUri },
          { "client_id", $"{adapter.ConsumerKey}@AMER.OAUTHAP" }
        } + string.Empty;

        page.Request += (sender, e) =>
        {
          if (e.Request.ResourceType == ResourceType.StyleSheet)
          {
            e.Request.AbortAsync();
            return;
          }

          e.Request.ContinueAsync();
        };

        await page.SetRequestInterceptionAsync(true);
        await page.GoToAsync($"{source}");
        await page.WaitForSelectorAsync(".accept.button");
        await page.TypeAsync("[name=su_username]", adapter.Username);
        await page.TypeAsync("[name=su_password]", adapter.Password);
        await page.ClickAsync(".accept.button");
        await nav;

        var verification = await page.WaitForSelectorAsync(".alternates .row");

        if (verification is not null)
        {
          nav = page.WaitForNavigationAsync();

          await page.ClickAsync(".alternates .row");
          await page.WaitForSelectorAsync("[name=init_secretquestion]");
          await page.ClickAsync("[name=init_secretquestion]");
          await nav;

          nav = page.WaitForNavigationAsync();

          await page.WaitForSelectorAsync("[name=su_secretquestion]");
          await page.TypeAsync("[name=su_secretquestion]", adapter.Answer);
          await page.ClickAsync(".accept.button");
          await nav;

          nav = page.WaitForNavigationAsync();

          await page.WaitForSelectorAsync("[name=su_trustthisdevice]");
          await page.ClickAsync("[name=su_trustthisdevice]");
          await page.WaitForSelectorAsync("input[type='radio']:checked");
          await page.ClickAsync(".accept.button");
          await nav;
        }

        nav = page.WaitForNavigationAsync();

        await page.ClickAsync(".accept.button");
        await nav;

        var codeSource = page.Target.Url;
        var securityCode = codeSource.ToPairs().Get("code");
        var userModel = await Authenticate(adapter, securityCode);

        userModel.SecurityCode = securityCode;
        userModel.ConsumerKey = adapter.ConsumerKey;
        userModel.ExpirationDate = DateTime.UtcNow.AddSeconds(userModel.ExpiresIn).Ticks;

        SaveUser(nameof(Authenticator), userModel);

        return userModel;
      }
    }

    /// <summary>
    /// Get API token
    /// </summary>
    /// <param name="adapter"></param>
    /// <param name="securityCode"></param>
    /// <returns></returns>
    public async Task<UserModel> Authenticate(Adapter adapter, string securityCode)
    {
      var props = new Dictionary<string, string>
      {
        { "grant_type", "authorization_code" },
        { "access_type", "offline" },
        { "client_id", $"{adapter.ConsumerKey}@AMER.OAUTHAP" },
        { "redirect_uri", adapter.SignInLocalUri },
        { "code", securityCode }
      };

      var query = new HttpRequestMessage(HttpMethod.Post, adapter.SignInApiUri)
      {
        Content = new FormUrlEncodedContent(props)
      };

      return (await InstanceService<Service>.Instance.Send<UserModel>(query)).Data;
    }
  }
}
