using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Link.AspireAppHost.Security;

/// <summary>A local Keycloak and what the Admin BFF and the Create user command need from it.</summary>
internal sealed record LinkKeycloak(
    IResourceBuilder<ContainerResource> Container,
    IResourceBuilder<ParameterResource> AdminPassword,
    IResourceBuilder<ParameterResource> ClientSecret)
{
    public const string Realm = "link";
    public const string ClientId = "link-admin-bff";
    public const string AdminUser = "admin";

    /// <summary>
    /// <c>localhost</c>, not 127.0.0.1: the address must match the development certificate's name, and it
    /// is the issuer in every token, so the Admin BFF and the browser must use the same one.
    /// </summary>
    public static readonly string BaseUrl = $"https://localhost:{Ports.Keycloak}";

    public static readonly string Authority = $"{BaseUrl}/realms/{Realm}";

    /// <summary>Gets an access token for Keycloak's admin API, as the bootstrap admin of the master realm.</summary>
    public async Task<string> GetAdminTokenAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var password = await AdminPassword.Resource.GetValueAsync(cancellationToken);
        using var response = await http.PostAsync(
            $"{BaseUrl}/realms/master/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = AdminUser,
                ["password"] = password ?? ""
            }),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return token.GetProperty("access_token").GetString()!;
    }

    /// <summary>
    /// Creates a user in the <c>link</c> realm with a password that does not need changing. Returns
    /// Created, Conflict when the username or email is taken, or the failure Keycloak gave.
    /// </summary>
    public async Task<(HttpStatusCode Status, string Body)> CreateUserAsync(
        string firstName, string lastName, string email, string username, string password, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await GetAdminTokenAsync(http, cancellationToken));

        using var response = await http.PostAsJsonAsync($"{BaseUrl}/admin/realms/{Realm}/users", new
        {
            username,
            email,
            firstName,
            lastName,
            enabled = true,
            emailVerified = true,
            credentials = new[] { new { type = "password", value = password, temporary = false } }
        }, cancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
