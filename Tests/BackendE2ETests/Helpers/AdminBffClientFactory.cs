using RestSharp;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

public static class AdminBffClientFactory
{
    private static RestClient? _instance;
    private static readonly object Lock = new();

    public static RestClient Create()
    {
        if (_instance != null)
            return _instance;

        lock (Lock)
        {
            _instance ??= CreateAuthenticatedClient();
        }

        return _instance;
    }

    private static RestClient CreateAuthenticatedClient()
    {
        var client = new RestClient(TestConfig.AdminBffBase);

        if (TestConfig.AdminBffOAuth.ShouldAuthenticate)
        {
            string token = AuthHelper.GetBearerToken(TestConfig.AdminBffOAuth);

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Could not get token for user");

            client.AddDefaultHeader("Authorization", "Bearer " + token);
        }

        return client;
    }
}
