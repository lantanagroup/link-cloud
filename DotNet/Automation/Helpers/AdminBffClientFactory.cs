using LantanaGroup.Link.Automation.Configuration;
using RestSharp;

namespace LantanaGroup.Link.Automation.Helpers;

public static class AdminBffClientFactory
{
    private static RestClient? _instance;
    private static readonly object Lock = new();

    public static RestClient Create(AutomationConfig config)
    {
        if (_instance != null)
            return _instance;

        lock (Lock)
        {
            _instance ??= CreateAuthenticatedClient(config);
        }

        return _instance;
    }

    /// <summary>
    /// Resets the cached client instance. Useful when switching configurations
    /// between test runs or when targeting a different environment.
    /// </summary>
    public static void Reset()
    {
        lock (Lock)
        {
            _instance = null;
        }
    }

    private static RestClient CreateAuthenticatedClient(AutomationConfig config)
    {
        var client = new RestClient(config.AdminBffBase);

        if (config.AdminBffOAuth.ShouldAuthenticate)
        {
            string token = AuthHelper.GetBearerToken(config.AdminBffOAuth);

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Could not get token for user");

            client.AddDefaultHeader("Authorization", "Bearer " + token);
        }

        return client;
    }
}
