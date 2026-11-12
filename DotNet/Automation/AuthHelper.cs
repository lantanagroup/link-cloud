using LantanaGroup.Automation.Configuration;
using RestSharp;
using System.Text;
using System.Text.Json;

namespace LantanaGroup.Automation;

/// <summary>
/// Generic authentication helpers for OAuth and Basic auth flows.
/// </summary>
public static class AuthHelper
{
    public static string GetBasicAuthorization(BasicAuthConfig config)
    {
        if (string.IsNullOrEmpty(config.Username))
            throw new InvalidOperationException("Basic auth username is not configured");

        if (string.IsNullOrEmpty(config.Password))
            throw new InvalidOperationException("Basic auth password is not configured");

        var authString = $"{config.Username}:{config.Password}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
    }

    public static string GetBearerToken(OAuthConfig config)
    {
        RequirePasswordGrant(config);

        var client = new RestClient(config.TokenEndpoint!);
        var response = client.Execute(TokenRequest(config));
        return ReadAccessToken(response);
    }

    /// <summary>
    /// Same password grant as <see cref="GetBearerToken(OAuthConfig)"/>, bounded by
    /// <paramref name="timeout"/> and <paramref name="cancellationToken"/>.
    /// RestSharp's synchronous execute uses a 100 second timeout and no caller token.
    /// </summary>
    public static async Task<string> GetBearerTokenAsync(
        OAuthConfig config,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        RequirePasswordGrant(config);

        using var client = new RestClient(config.TokenEndpoint!);
        var request = TokenRequest(config);
        request.Timeout = timeout;

        var response = await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return ReadAccessToken(response);
    }

    private static void RequirePasswordGrant(OAuthConfig config)
    {
        if (string.IsNullOrEmpty(config.TokenEndpoint))
            throw new InvalidOperationException("OAuth token endpoint is not configured");

        if (string.IsNullOrEmpty(config.ClientId))
            throw new InvalidOperationException("OAuth client ID is not configured");

        if (string.IsNullOrEmpty(config.Username))
            throw new InvalidOperationException("OAuth username is not configured");

        if (string.IsNullOrEmpty(config.Password))
            throw new InvalidOperationException("OAuth password is not configured");
    }

    private static RestRequest TokenRequest(OAuthConfig config)
    {
        var request = new RestRequest("", Method.Post);
        request.AddParameter("grant_type", "password");
        request.AddParameter("client_id", config.ClientId);
        if (!string.IsNullOrEmpty(config.ClientSecret))
            request.AddParameter("client_secret", config.ClientSecret);
        request.AddParameter("username", config.Username);
        request.AddParameter("password", config.Password);
        request.AddParameter("scope", config.Scope);
        return request;
    }

    private static string ReadAccessToken(RestResponse response)
    {
        if (!response.IsSuccessful)
            throw new InvalidOperationException($"Failed to obtain OAuth token: {response.ErrorMessage}");

        var responseModel = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response.Content!);

        if (!responseModel!.ContainsKey("access_token"))
            throw new InvalidOperationException("OAuth token response does not contain access token");

        return responseModel["access_token"].GetString()!;
    }
}
