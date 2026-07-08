using LantanaGroup.Link.Automation.Link.Configuration;
using RestSharp;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.Automation.Link.Helpers;

public static class AdminBffClientFactory
{
    private const string AdminBffHttpClientName = "Automation.AdminBff";

    public static RestClient Create(AutomationConfig config, IHttpClientFactory httpClientFactory)
    {
        return CreateAuthenticatedClient(config, httpClientFactory);
    }

    public static void Reset()
    {
    }

    private static RestClient CreateAuthenticatedClient(AutomationConfig config, IHttpClientFactory httpClientFactory)
    {
        var httpClient = httpClientFactory.CreateClient(AdminBffHttpClientName);

        if (httpClient.BaseAddress == null)
            httpClient.BaseAddress = new Uri(config.AdminBffBase);

        if (config.AdminBffOAuth.ShouldAuthenticate)
        {
            string token = AuthHelper.GetBearerToken(config.AdminBffOAuth);

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Could not get token for user");

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            httpClient.DefaultRequestHeaders.Authorization = null;
        }

        return new RestClient(httpClient, disposeHttpClient: false);
    }
}
