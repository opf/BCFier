﻿using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OpenProject.WebViewIntegration
{
  public static class OpenProjectInstanceValidator
  {
    private static readonly IServiceProvider _services;

    static OpenProjectInstanceValidator()
    {
      // We're using HttpClientFactory to ensure that we don't hit any problems with
      // port exhaustion or stale DNS entries in long-lived HttpClients
      var serviceCollection = new ServiceCollection();
      serviceCollection.AddHttpClient(nameof(OpenProjectInstanceValidator))
        .ConfigureHttpMessageHandlerBuilder(h =>
        {
          if (h.PrimaryHandler is HttpClientHandler httpClientHandler)
          {
            // It defaults to true, but let's ensure it stays that way😀
            httpClientHandler.AllowAutoRedirect = true;
          }
        })
        ;
      _services = serviceCollection.BuildServiceProvider();
    }

    public static async Task<(bool isValid, string instanceBaseUrl)> IsValidOpenProjectInstanceAsync(string instanceNameOrUrl)
    {
      var instanceUrl = GetInstanceUrl(instanceNameOrUrl);
      if (string.IsNullOrWhiteSpace(instanceUrl))
      {
        return (false, null);
      }

      var response = await GetHttpResponseAsync(instanceUrl);
      if (response == null)
      {
        // This means there was an Http error, e.g. unable to make a connection
        // or a failure to resolve the domain. So it's either not reachable or
        // not a valid domain, which both warrants a false return from this method
        return (false, null);
      }

      switch (response.StatusCode)
      {
        case HttpStatusCode.OK:
        case HttpStatusCode.Unauthorized:
          var validationResult = await IsLikelyOpenProjectHttpResponseContentAsync(response);
          if (validationResult)
          {
            return (true, Regex.Replace(instanceUrl.ToLower().TrimEnd('/'), "/api/v3$", string.Empty));
          }
          break;
      }

      return (false, null);
    }

    private static string GetInstanceUrl(string instanceNameOrUrl)
    {
      if (Uri.TryCreate(instanceNameOrUrl, UriKind.Absolute, out var _)
        && Regex.IsMatch(instanceNameOrUrl.ToLower(), "^https?://"))
      {
        return instanceNameOrUrl;
      }

      var subDomainRegexPattern = "^[a-zA-Z0-9-]+$";
      if (Regex.IsMatch(instanceNameOrUrl, subDomainRegexPattern))
      {
        return $"https://{instanceNameOrUrl}.openproject.com/api/v3";
      }

      if (Uri.TryCreate($"https://{instanceNameOrUrl}", UriKind.Absolute, out var instanceUri))
      {
        if (!instanceUri.PathAndQuery.TrimEnd('/').EndsWith("/api/v3", StringComparison.InvariantCultureIgnoreCase))
        {
          return $"https://{instanceNameOrUrl.TrimEnd('/')}/api/v3";
        }

        return $"https://{instanceNameOrUrl}";
      }

      return null;
    }

    private static async Task<HttpResponseMessage> GetHttpResponseAsync(string instanceUrl)
    {
      using var httpClient = _services
        .GetRequiredService<IHttpClientFactory>()
        .CreateClient(nameof(OpenProjectInstanceValidator));
      try
      {
        var response = await httpClient.GetAsync(instanceUrl);
        return response;
      }
      catch
      {
        return null;
      }
    }

    private static async Task<bool> IsLikelyOpenProjectHttpResponseContentAsync(HttpResponseMessage httpResponse)
    {
      var responseContent = await httpResponse.Content.ReadAsStringAsync();
      try
      {
        var jObject = JObject.Parse(responseContent);
        if (httpResponse.IsSuccessStatusCode)
        {
          return IsLikelyOpenProjectInstanceRoot(jObject);
        }

        return IsLikelyOpenProjectErrorInstance(jObject);
      }
      catch
      {
        return false;
      }
    }

    private static bool IsLikelyOpenProjectInstanceRoot(JObject jObject)
    {
      return (jObject["_type"]?.ToString().Equals("Root", StringComparison.OrdinalIgnoreCase) ?? false)
        && !string.IsNullOrWhiteSpace(jObject["instanceName"]?.ToString());
    }

    private static bool IsLikelyOpenProjectErrorInstance(JObject jObject)
    {
      return (jObject["_type"]?.ToString().Equals("Error", StringComparison.OrdinalIgnoreCase) ?? false)
        && !string.IsNullOrWhiteSpace(jObject["errorIdentifier"]?.ToString());
    }
  }
}
