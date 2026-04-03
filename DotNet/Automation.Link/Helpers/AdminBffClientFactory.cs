using LantanaGroup.Link.Automation.Configuration;
using RestSharp;

namespace LantanaGroup.Link.Automation.Helpers;

public static class AdminBffClientFactory
{
    public static RestClient Create(AutomationConfig config)
    {
        return CreateAuthenticatedClient(config);
    }

    public static void Reset()
    {
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
